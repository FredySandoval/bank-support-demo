#!/usr/bin/env python3
"""Bounded, read-only load test of the local demo. Not a DDoS simulation.

Run: python3 scripts/load-test.py
Uses only the Python standard library. Never submits logins or transfers.
"""

import time
import urllib.error
import urllib.request
from collections import Counter
from concurrent.futures import FIRST_COMPLETED, ThreadPoolExecutor, wait

URL = "http://127.0.0.1:8081/login"
RATE = 5
DURATION = 30
WORKERS = 5
TIMEOUT = 3


def request():
    started = time.monotonic()
    try:
        with urllib.request.urlopen(URL, timeout=TIMEOUT) as response:
            response.read()
            status = response.status
    except urllib.error.HTTPError as error:
        status = error.code
        error.close()
    except (OSError, urllib.error.URLError):
        status = "connection error/timeout"
    return status, time.monotonic() - started


def main():
    results = []
    pending = set()
    submitted = 0
    started = time.monotonic()
    deadline = started + DURATION
    next_request = started
    reason = "Time/request limit reached"

    print(f"Target: {URL}")
    print(f"Limits: {RATE} requests/sec, {DURATION} seconds, {WORKERS} concurrent")
    print("Read-only test. Monitor docker stats; press Ctrl+C to stop.", flush=True)

    def collect(futures):
        for future in futures:
            results.append(future.result())

    with ThreadPoolExecutor(max_workers=WORKERS) as pool:
        try:
            while submitted < RATE * DURATION:
                done = {future for future in pending if future.done()}
                pending -= done
                collect(done)

                failures = sum(status != 200 for status, _ in results)
                slow = sum(seconds >= 2 for _, seconds in results)
                if failures >= 5 or slow >= 5:
                    reason = "Safety stop: five errors or five responses taking >=2 seconds"
                    break

                now = time.monotonic()
                if now >= deadline:
                    break
                if len(pending) >= WORKERS:
                    done, pending = wait(
                        pending, timeout=min(0.2, deadline - now),
                        return_when=FIRST_COMPLETED,
                    )
                    collect(done)
                    continue
                if now < next_request:
                    time.sleep(min(next_request - now, deadline - now))
                    continue

                pending.add(pool.submit(request))
                submitted += 1
                # Never send catch-up bursts after a delay.
                next_request = time.monotonic() + 1 / RATE
        except KeyboardInterrupt:
            reason = "Stopped by operator"

        print(f"\n{reason}. Waiting for in-flight requests...", flush=True)
        collect(pending)

    elapsed = time.monotonic() - started
    latencies = sorted(seconds * 1000 for _, seconds in results)
    print(f"Completed: {len(results)} requests in {elapsed:.1f}s")
    print("Responses:", dict(Counter(status for status, _ in results)))
    if latencies:
        print(f"Average latency: {sum(latencies) / len(latencies):.0f} ms")
        print(f"95th percentile: {latencies[int((len(latencies) - 1) * 0.95)]:.0f} ms")
        print(f"Maximum latency: {max(latencies):.0f} ms")
    print("This tests the local app, not Cloudflare or real DDoS resilience.")
    return 1 if any(status != 200 for status, _ in results) else 0


if __name__ == "__main__":
    raise SystemExit(main())
