# Bank Support Demo

Minimal banking-oriented support demo: ASP.NET Core MVC, C#/.NET 10, EF Core,
SQLite, cookie authentication, locally bundled Bootstrap, and vanilla JavaScript.
Not a real banking system. Use only demo data and a trusted demo environment.

## Docker Compose (recommended)

Install Docker Engine and the Compose plugin, then from this directory:

```bash
mkdir -p data .secrets
chmod 700 .secrets
# First setup only; do not overwrite an existing password:
(umask 077; set -C; openssl rand -base64 32 > .secrets/admin_password)
docker compose up -d --build
```

The app listens at http://127.0.0.1:8081 on the Docker host only (container port
8080). Port 8080 on this demo VM is already used by another app.

For the host-level cloudflared service, configure the public hostname's HTTP
service as `http://127.0.0.1:8081`. No inbound firewall ports are needed for the
tunnel. Protect the hostname with **Cloudflare Access** before exposing this demo:
this is a demo, not a hardened banking application. Tunnel routing is configured separately.
If cloudflared runs in Docker instead, attach it to the same Docker network and
use `http://bank-support-demo:8080`; loopback inside that container is not the host.
Login username: **admin**. Read the password locally from `.secrets/admin_password`.
This file is excluded from Git and Docker builds and mounted as a Compose secret.
Never commit it. The database stores only a password hash.

Startup requires `AdminPasswordFile` pointing to a file containing at least 16
characters. It synchronizes the admin password on every startup, including existing
databases, without resetting balances. To rotate it, replace the secret file and run
`docker compose up -d --force-recreate`. Password changes alone do not revoke existing
cookies; to revoke all sessions on future rotations, change the data-protection
application name in `Program.cs` and rebuild. This deployment changes that scope to
invalidate sessions from before the hardcoded credential was retired.

```bash
docker compose logs -f
docker compose down
# Start again with the same database:
docker compose up -d
```

If a container named `bank-support-demo` was previously created with `docker run`,
remove it before switching to Compose:

```bash
docker stop bank-support-demo
docker rm bank-support-demo
```

This preserves the bind-mounted `data/` directory. Compose builds for the host
architecture automatically; run the same commands directly on your ARM64 VM.
Do not deploy an x86_64-only image to ARM64.

## Docker without Compose

```bash
docker build -t bank-support-demo:latest .
mkdir -p data
docker run -d \
  --name bank-support-demo \
  -p 127.0.0.1:8081:8080 \
  -v "$(pwd)/data:/app/data" \
  -v "$(pwd)/.secrets/admin_password:/run/secrets/admin_password:ro" \
  -e AdminPasswordFile=/run/secrets/admin_password \
  --restart unless-stopped \
  bank-support-demo:latest
```

## Local .NET development

Install the .NET 10 SDK:

```bash
dotnet restore
AdminPasswordFile="$PWD/.secrets/admin_password" dotnet run --urls http://localhost:8080
```

Local SQLite path is `data/bank.db`. Docker overrides the connection string with
`Data Source=/app/data/bank.db`. Override it using
`ConnectionStrings__DefaultConnection` if necessary; its parent directory must exist.

## URLs

- `/login`: demo login
- `/accounts`: account balances
- `/transfers/new`: transfer form
- `/incidents`: support-visible failures, UTC timestamps
- `/health`: anonymous health check, 200 when SQLite can be queried; 503 otherwise

All application pages other than login and health require authentication.
Logout is a CSRF-protected POST in the navigation.

## Demo flows

Initial balances: Alice Johnson 5,000.00; Bob Smith 2,500.00; Demo Company 10,000.00.

**Insufficient funds (try first on fresh data):**

1. Login and transfer 10,000.00 from Alice to Bob.
2. See `Insufficient funds` on the form.
3. Open `/incidents`: a `TransferFailure` is recorded.
4. Inspect logs for a warning explaining the rejection.
5. Open `/accounts`: balances are unchanged.

**Successful transfer:** transfer 100.00 from Alice to Bob. Expect a success
message, Alice 4,900.00 and Bob 2,600.00. A `Completed` transfer is persisted.
Repeat attempts are separate transfers; this demo has no idempotency keys.

## Logs and troubleshooting

```bash
docker logs -f bank-support-demo
curl -i http://127.0.0.1:8081/health
docker inspect bank-support-demo --format '{{.State.Status}}'
```

Support flow: user report → UI error → incidents → logs → account balances.
Logs use structured ILogger messages and stdout/stderr. No passwords or cookies
are logged. Unexpected transfer errors roll back and attempt to save an incident
outside the original transaction. If SQLite itself is unavailable, incident
persistence may also fail; the error remains in logs. Check balances before
retrying an operation whose outcome could not be confirmed.

For SQLite inspection, stop the app first and use a host SQLite client:

```bash
docker compose stop
sqlite3 data/bank.db 'SELECT Id, Owner, Balance / 100.0 AS Balance FROM Accounts;'
sqlite3 data/bank.db 'SELECT Id, Status, Amount / 100.0 AS Amount FROM Transfers;'
docker compose start
```

Money is stored as integer cents, not floating point. Transfers accept up to two
decimal places and a maximum of 1,000,000,000; destination balances have the same limit.

If startup fails, inspect logs and verify the data directory is writable and port
8081 is free. An `overlay` module error from Docker is a host/kernel problem, not
an app error; after a kernel upgrade, reboot and restart Docker.

## Persistence and security boundaries

`./data` is bind-mounted to `/app/data`. Database and cookie protection keys survive
container recreation. Seeds are idempotent and do not reset existing balances.
To reset the demo, stop the container and remove the database plus any `-wal` and
`-shm` sidecars from `data/` (this permanently deletes demo transactions/incidents).
Back up the directory only while the app is stopped, or use SQLite's backup API.

The demo container uses the image's default root user for straightforward bind-mount
permissions. Data-protection keys are persisted without at-rest encryption; protect
the host directory. Origin HTTP is a deliberate demo limitation.
Do not expose it without an access-control layer or put real financial information in it.
With Cloudflare Tunnel, terminate public HTTPS at Cloudflare and restrict access
using Cloudflare Access; keep the origin bound to loopback.
Static assets are public; all business endpoints require cookies. MVC POSTs enforce
antiforgery tokens. HTTPS termination and production hardening are out of scope.

## Architecture

Controllers → services → AppDbContext for authentication and transfers. Read-only
accounts/incidents controllers query EF directly. SQLite immediate write transactions
serialize writers before balances are read. Debit, credit, and completed transfer
are committed atomically. Rejections produce incidents with no transfer ID; failed
Transfer rows are intentionally not stored (invalid account IDs may not exist).

Database creation uses `EnsureCreated`, not migrations. Schema changes require a
fresh demo database or a future migration workflow.

## Validation

The Docker image builds and runs on x86_64. Automated HTTP smoke tests cover login,
protected pages, CSRF, transfer validation, insufficient funds without balance
changes, incidents, successful transfer, and logout. Database checks verified
hashed passwords, completed records, restart persistence, and idempotent seeds.
Linux ARM64 cross-publish succeeds with native SQLite included. Native ARM64 Docker build and runtime were also validated on the demo VM,
including the full HTTP smoke test against a disposable database.

Run the smoke test only with a **fresh disposable database** (it changes balances):

```bash
docker run -d --name bank-support-test -p 127.0.0.1:18080:8080 \
  -v "$PWD/.secrets/admin_password:/run/secrets/admin_password:ro" \
  -e AdminPasswordFile=/run/secrets/admin_password bank-support-demo:latest
# Wait for /health to return 200, then:
AdminPasswordFile="$PWD/.secrets/admin_password" python3 scripts/smoke-test.py http://localhost:18080
docker rm -f bank-support-test
```
