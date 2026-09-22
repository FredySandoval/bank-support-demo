#!/usr/bin/env python3
"""Bounded, read-only load test. Not a DDoS simulation.

Local: python3 scripts/load-test.py
External: python3 scripts/load-test.py --external
Optional Access credentials: CF_ACCESS_CLIENT_ID and CF_ACCESS_CLIENT_SECRET.
Credentials are never printed and redirects are not followed.
Uses only the Python standard library. Never submits logins or transfers.
"""

import argparse
import os
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


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        # Do not forward Access credentials or load an identity-provider page.
        return None


def request(url, headers):
    started = time.monotonic()
    try:
        opener = urllib.request.build_opener(NoRedirect())
        req = urllib.request.Request(url, headers=headers)
        with opener.open(req, timeout=TIMEOUT) as response:
            response.read()
            status = response.status
    except urllib.error.HTTPError as error:
        status = error.code
        error.close()
    except (OSError, urllib.error.URLError):
        status = "connection error/timeout"
    return status, time.monotonic() - started


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--external", action="store_true",
                        help="Test https://bank-support-demo.fredy.dev/login for 10 seconds")
    args = parser.parse_args()
    url = "https://bank-support-demo.fredy.dev/login" if args.external else URL
    duration = 10 if args.external else DURATION
    headers = {"User-Agent": "BankSupportDemo-BoundedLoadTest/1.0"}
    if args.external:
        client_id = os.environ.get("CF_ACCESS_CLIENT_ID")
        client_secret = os.environ.get("CF_ACCESS_CLIENT_SECRET")
        if bool(client_id) != bool(client_secret):
            parser.error("Set both CF_ACCESS_CLIENT_ID and CF_ACCESS_CLIENT_SECRET, or neither.")
        if client_id and client_secret:
            headers["CF-Access-Client-Id"] = client_id
            headers["CF-Access-Client-Secret"] = client_secret
        else:
            print("No Access token configured; Access may block or redirect requests.")

    results = []
    pending = set()
    submitted = 0
    started = time.monotonic()
    deadline = started + duration
    next_request = started
    reason = "Time/request limit reached"

    print(f"Target: {url}")
    print(f"Limits: {RATE} requests/sec, {duration} seconds, {WORKERS} concurrent")
    print("Read-only test. Monitor docker stats; press Ctrl+C to stop.", flush=True)

    def collect(futures):
        for future in futures:
            results.append(future.result())

    with ThreadPoolExecutor(max_workers=WORKERS) as pool:
        try:
            while submitted < RATE * duration:
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

                pending.add(pool.submit(request, url, headers))
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
    if args.external:
        print("3xx: redirect (possibly Access); 403: denied; 429: rate limited.")
        print("A 200 alone does not prove the request reached the app; check origin logs.")
        print("Traffic crosses Cloudflare; analytics may be delayed or sampled.")
    else:
        print("Local mode bypasses Cloudflare.")
    print("This bounded load test does not establish real DDoS resilience.")
    return 1 if any(status != 200 for status, _ in results) else 0


if __name__ == "__main__":
    raise SystemExit(main())
