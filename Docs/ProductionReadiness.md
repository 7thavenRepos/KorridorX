# KorridorX Production Readiness

## Runtime endpoints

- `GET /health/live` confirms that the API process is running.
- `GET /health/ready` confirms that PostgreSQL is reachable.
- `GET /api/health` remains available as the application-level health response.
- Every response includes `X-Correlation-ID`. A valid inbound value is preserved; otherwise, the API generates one.

Use `/health/live` for container/process restarts and `/health/ready` for load-balancer traffic eligibility.

## Required production configuration

Production startup fails when required configuration is missing or unsafe. Supply secrets through environment variables or a managed secret store rather than committing them to JSON files.

Required values include:

```text
ConnectionStrings__DefaultConnection
Jwt__Issuer
Jwt__Audience
Jwt__Key
AllowedHosts
Hosting__AllowedOrigins__0
Hosting__DataProtectionKeysPath
```

When Blaaiz is enabled, also provide:

```text
Blaaiz__ClientId
Blaaiz__ClientSecret
Blaaiz__WebhookSigningSecret
```

When notification delivery is enabled, provide the SMTP configuration under `NotificationDelivery__Smtp__...`.

`appsettings.Production.json` intentionally leaves `Hosting:AllowedOrigins` empty. Production will not start until the deployment supplies the real frontend origin.

## Reverse proxy configuration

KorridorX accepts forwarded protocol and client-IP headers only when trusted proxy IP addresses are explicitly configured:

```text
Hosting__TrustedProxies__0=10.0.0.10
Hosting__ForwardLimit=1
```

Do not add arbitrary public IP addresses and do not trust forwarded headers from every network.

## Data Protection keys

Production requires a persistent Data Protection key directory. In containers, mount a persistent volume at:

```text
/var/lib/korridorx/keys
```

The keys must be backed up and readable only by the application identity.

## Database migrations

Do not automatically migrate the database during API startup. Generate and review an idempotent script during CI:

```powershell
dotnet ef migrations script --idempotent --output artifacts/migrations.sql
```

Apply the reviewed script as a separate deployment step before shifting traffic to the new API version.

The migration `20260806191138_AddBusinessFundingWalletLedgerCollections` was corrected to contain only the business-wallet, ledger, reservation, and notification-read changes. It no longer replays schema operations from earlier migrations.

## Backup and restore

PowerShell:

```powershell
$env:PGPASSWORD = "<database-password>"
./scripts/db/backup.ps1 -HostName localhost -Database korridorx -Username korridorx

./scripts/db/restore.ps1 `
  -HostName localhost `
  -Database korridorx `
  -Username korridorx `
  -BackupFile ./backups/korridorx-YYYYMMDD-HHMMSS.dump `
  -ConfirmRestore
```

Bash:

```bash
export PGHOST=localhost
export PGPORT=5432
export PGDATABASE=korridorx
export PGUSER=korridorx
export PGPASSWORD='<database-password>'

./scripts/db/backup.sh
./scripts/db/restore.sh ./backups/korridorx-YYYYMMDD-HHMMSS.dump --confirm
```

Test restores regularly against a non-production database. A backup that has never been restored is not yet proven.

## Docker

Copy `.env.example` to `.env`, replace every placeholder, then run:

```bash
docker compose build
docker compose up -d
```

The API container runs as the non-root `app` user. PostgreSQL data and Data Protection keys use separate persistent volumes.

## Logging and telemetry

Production uses structured JSON console logs. Logs include correlation scopes and HTTP completion records without query-string values. Slow requests and server failures are emitted as warnings.

`KorridorXTelemetry` exposes an `ActivitySource` and `Meter` so OpenTelemetry exporters can be added later without changing domain services. Current instruments include HTTP request duration and 5xx request counts.

## Deployment sequence

1. Build and test the exact commit.
2. Generate and review the idempotent migration script.
3. Create a verified database backup.
4. Apply migrations.
5. Deploy the new container image.
6. Confirm `/health/live` and `/health/ready`.
7. Run authentication, quote, collection, and payout smoke tests.
8. Monitor error rates, slow requests, provider failures, and reconciliation queues.
9. Shift traffic gradually where the hosting platform supports it.
10. Keep the previous image available for application rollback. Database rollback should use forward-fix migrations unless a tested restore is explicitly required.
