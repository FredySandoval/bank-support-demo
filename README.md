# Bank Support Demo

Minimal banking-oriented support demo: ASP.NET Core MVC, C#/.NET 10, EF Core,
SQLite, cookie authentication, locally bundled Bootstrap, and vanilla JavaScript.
Not a real banking system. Use only demo data and a trusted demo environment.

## Docker Compose (recommended)

Install Docker Engine and the Compose plugin, then from this directory:

```bash
mkdir -p data
docker compose up -d --build
```

Open http://localhost:8080 (or http://SERVER_IP:8080).
Login: **admin / Demo123!**. The seeded password is stored as a hash.

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
  -p 8080:8080 \
  -v "$(pwd)/data:/app/data" \
  --restart unless-stopped \
  bank-support-demo:latest
```

## Local .NET development

Install the .NET 10 SDK:

```bash
dotnet restore
dotnet run --urls http://localhost:8080
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
curl -i http://localhost:8080/health
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
8080 is free. An `overlay` module error from Docker is a host/kernel problem, not
an app error; after a kernel upgrade, reboot and restart Docker.

## Persistence and security boundaries

`./data` is bind-mounted to `/app/data`. Database and cookie protection keys survive
container recreation. Seeds are idempotent and do not reset existing balances.
To reset the demo, stop the container and remove the database plus any `-wal` and
`-shm` sidecars from `data/` (this permanently deletes demo transactions/incidents).
Back up the directory only while the app is stopped, or use SQLite's backup API.

The demo container uses the image's default root user for straightforward bind-mount
permissions. Data-protection keys are persisted without at-rest encryption; protect
the host directory. HTTP and public demo credentials are deliberate demo limitations.
Do not expose it to the public internet or put real financial information in it.
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
Linux ARM64 cross-publish succeeds with native SQLite included. Official .NET images
support ARM64; native ARM64 Docker build/runtime validation must still be done on
an ARM64 host.

Run the smoke test only with a **fresh disposable database** (it changes balances):

```bash
docker run -d --name bank-support-test -p 127.0.0.1:18080:8080 bank-support-demo:latest
# Wait for /health to return 200, then:
python3 scripts/smoke-test.py http://localhost:18080
docker rm -f bank-support-test
```
