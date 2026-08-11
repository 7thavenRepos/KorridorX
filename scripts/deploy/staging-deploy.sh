#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$script_dir/../.." && pwd)"
cd "$project_dir"

env_file=".env.staging"
compose_file="docker-compose.staging.yml"
migration_file="artifacts/migrations.sql"

if [[ ! -f "$env_file" ]]; then
  echo "Missing $env_file. Copy .env.staging.example and replace every placeholder." >&2
  exit 1
fi

if [[ ! -f "$migration_file" ]]; then
  echo "Missing $migration_file. Download migrations.sql from the successful GitHub CI artifact." >&2
  exit 1
fi

if grep -Eq 'replace-with|example\.com' "$env_file"; then
  echo "$env_file still contains placeholder values." >&2
  exit 1
fi

env_mode="$(stat -c '%a' "$env_file")"
if [[ "$env_mode" != "600" ]]; then
  echo "$env_file must be readable only by its owner. Run: chmod 600 $env_file" >&2
  exit 1
fi

compose=(docker compose --env-file "$env_file" -f "$compose_file")
"${compose[@]}" config --quiet

mkdir -p backups
chmod 700 backups

"${compose[@]}" up -d --wait --wait-timeout 120 postgres

if "${compose[@]}" ps --status running --services | grep -qx postgres; then
  timestamp="$(date -u +%Y%m%d-%H%M%S)"
  backup_file="backups/korridorx-staging-$timestamp.dump"
  "${compose[@]}" exec -T postgres sh -c \
    'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --format=custom --compress=9 --no-owner --no-privileges' \
    > "$backup_file"
  chmod 600 "$backup_file"
  echo "Database backup created: $backup_file"
fi

if docker image inspect korridorx-api:staging >/dev/null 2>&1; then
  docker image tag korridorx-api:staging korridorx-api:staging-rollback
  echo "Previous API image preserved as korridorx-api:staging-rollback"
fi

echo "Applying reviewed idempotent migration script..."
"${compose[@]}" exec -T postgres sh -c \
  'PGPASSWORD="$POSTGRES_PASSWORD" psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set ON_ERROR_STOP=1' \
  < "$migration_file"

"${compose[@]}" build --pull api
"${compose[@]}" up -d --no-deps api

api_port="$(sed -n 's/^API_PORT=//p' "$env_file" | tail -n 1)"
api_port="${api_port:-8080}"
allowed_host="$(sed -n 's/^ALLOWED_HOSTS=//p' "$env_file" | tail -n 1)"

if [[ -z "$allowed_host" ]]; then
  echo "ALLOWED_HOSTS is missing from $env_file." >&2
  exit 1
fi

for attempt in {1..30}; do
  if curl --silent --fail \
      --header "Host: $allowed_host" \
      --header 'X-Forwarded-Proto: https' \
      "http://127.0.0.1:${api_port}/health/ready" >/dev/null; then
    echo "KorridorX staging API is ready."
    "${compose[@]}" ps
    exit 0
  fi
  sleep 2
done

echo "Staging readiness check failed." >&2
"${compose[@]}" logs --tail=200 api >&2
exit 1
