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

Use separate generated values for `POSTGRES_PASSWORD` and `JWT_KEY`.

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
./scripts/deploy/staging-deploy.sh
./scripts/deploy/staging-smoke-test.sh https://api.staging.korridorx.com
```

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

## 9. Updating staging

```bash
cd /opt/korridorx
git pull --ff-only origin master
```

Download the new successful CI migration artifact, replace `artifacts/migrations.sql`, then run the deploy and smoke-test scripts again.
