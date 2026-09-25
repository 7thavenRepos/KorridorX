# KorridorX Ubuntu Staging Deployment

This runbook deploys KorridorX to an Ubuntu server with Docker Compose and a host-installed Nginx reverse proxy. PostgreSQL is private to the Docker network and the API listens only on `127.0.0.1`.

## 1. DNS and firewall

Create an A/AAAA record for the staging API hostname. Allow inbound SSH, HTTP, and HTTPS only. Do not expose ports 5432 or 8080 publicly.

## 2. Install server dependencies

```bash
sudo apt update
sudo apt install -y ca-certificates curl git nginx certbot python3-certbot-nginx
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
```

Sign out and back in after adding the user to the Docker group.

## 3. Prepare the application

```bash
sudo mkdir -p /opt/korridorx
sudo chown "$USER":"$USER" /opt/korridorx
git clone https://github.com/7thavenRepos/KorridorX.git /opt/korridorx
cd /opt/korridorx
cp .env.staging.example .env.staging
chmod 600 .env.staging
```

Edit `.env.staging` and replace every placeholder. Generate secrets locally on the server:

```bash
openssl rand -base64 48
```

Use separate generated values for `POSTGRES_PASSWORD`, `JWT_KEY`, and
`MFA_CODE_REPLAY_PEPPER`.

Because Staging is a deployed environment, also configure the existing
`.env.staging` with:

- `REQUIRE_CONFIRMED_EMAIL=true`;
- `ACCOUNT_FRONTEND_BASE_URL=https://app.staging.korridorx.com`;
- `MFA_ENFORCE_FOR_PRIVILEGED_ROLES=true`;
- `NOTIFICATION_WORKER_ENABLED=true`;
- `SMTP_ENABLED=true`, plus a staging/sandbox SMTP host and sender address.

Existing staging identities are not replaced by this update. If a staging
administrator already exists, keep using that identity and its current MFA
enrollment.

## 4. Obtain the migration artifact

Download `korridorx-ci-artifacts` from the successful GitHub Actions run. Copy its `migrations.sql` file to:

```text
/opt/korridorx/artifacts/migrations.sql
```

The deploy script refuses to proceed without this reviewed CI artifact.

## 5. Configure Nginx and TLS

Obtain the first certificate before enabling the final TLS configuration:

```bash
sudo systemctl stop nginx
sudo certbot certonly --standalone -d api.staging.korridorx.com
sudo systemctl start nginx
```

The supplied Nginx configuration already uses `api.staging.korridorx.com`. Install it:

```bash
sudo cp deploy/nginx/korridorx-staging.conf /etc/nginx/sites-available/korridorx-staging
sudo ln -s /etc/nginx/sites-available/korridorx-staging /etc/nginx/sites-enabled/korridorx-staging
sudo nginx -t
sudo systemctl reload nginx
```

## 6. Deploy

```bash
cd /opt/korridorx
chmod +x scripts/deploy/*.sh
./scripts/deploy/staging-preflight.sh
./scripts/deploy/staging-deploy.sh
./scripts/deploy/staging-smoke-test.sh https://api.staging.korridorx.com
```

Swagger is intentionally enabled in Staging for UAT and integration testing.
The staging smoke test verifies both the Swagger UI and OpenAPI JSON return HTTP
200. Production keeps `Hosting:SwaggerEnabled=false`.

## 7. Inspect and operate

```bash
docker compose --env-file .env.staging -f docker-compose.staging.yml ps
docker compose --env-file .env.staging -f docker-compose.staging.yml logs -f --tail=200 api
curl -fsS https://api.staging.korridorx.com/health/live
curl -fsS https://api.staging.korridorx.com/health/ready
```

## 8. Application rollback

```bash
./scripts/deploy/staging-rollback.sh
```

Rollback restores the previously tagged API image. It does not reverse database migrations. Prefer a forward-fix migration; restore the verified pre-deployment backup only when an incident plan explicitly requires it.

### Staging API image build location

The staging EC2 host does not compile the .NET application. After tests and
migration validation pass, GitHub CI builds `korridorx-api:staging`, packages it
as `artifacts/korridorx-api-staging.tar.gz`, and transfers that image to the
staging host with `migrations.sql`.

The staging host only performs the database backup, preserves the current API
image as `korridorx-api:staging-rollback`, loads the CI-built image, applies the
reviewed migration artifact, recreates the API container with `--no-build`, and
runs readiness/smoke checks.

## 9. Automatic staging deployment

Successful pushes to `develop` deploy the exact CI-tested commit to the existing
staging host. The deployment job downloads the reviewed `migrations.sql`
artifact, copies it to `/opt/korridorx/artifacts/migrations.sql`, checks out the
exact commit SHA that passed CI, then runs the existing guarded deploy and public
smoke-test scripts.

Application environment values remain only in the server-owned
`/opt/korridorx/.env.staging`; the deployment workflow does not create, replace,
or upload that file.

The existing GitHub `staging` environment configuration is reused directly.

Environment secrets:

- `STAGING_SSH_PRIVATE_KEY`
- `STAGING_SSH_KNOWN_HOSTS`

Environment variables:

- `STAGING_SSH_HOST`
- `STAGING_SSH_USER`

`STAGING_HOST` may remain configured for other staging automation, but the SSH
deployment job uses `STAGING_SSH_HOST`.


## 10. Manual staging update / recovery


```bash
cd /opt/korridorx
git pull --ff-only origin develop
```

Download the new successful CI migration artifact, replace `artifacts/migrations.sql`, then run the deploy and smoke-test scripts again.
