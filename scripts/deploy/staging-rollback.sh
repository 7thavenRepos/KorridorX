#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$script_dir/../.." && pwd)"
cd "$project_dir"

if ! docker image inspect korridorx-api:staging-rollback >/dev/null 2>&1; then
  echo "No korridorx-api:staging-rollback image is available." >&2
  exit 1
fi

docker image tag korridorx-api:staging-rollback korridorx-api:staging

export KORRIDORX_IMAGE_TAG=staging
compose=(docker compose --env-file .env.staging -f docker-compose.staging.yml)
"${compose[@]}" up -d --no-deps --no-build --force-recreate api

api_port="$(sed -n 's/^API_PORT=//p' .env.staging | tail -n 1)"
api_port="${api_port:-8080}"
allowed_host="$(sed -n 's/^ALLOWED_HOSTS=//p' .env.staging | tail -n 1)"

if [[ -z "$allowed_host" ]]; then
  echo "ALLOWED_HOSTS is missing from .env.staging." >&2
  exit 1
fi

for attempt in {1..30}; do
  if curl --silent --fail \
      --header "Host: $allowed_host" \
      --header 'X-Forwarded-Proto: https' \
      "http://127.0.0.1:${api_port}/health/ready" >/dev/null; then
    echo "Previous API image restored and ready. Database migrations were not reversed."
    "${compose[@]}" ps
    exit 0
  fi
  sleep 2
done

echo "Rollback image started but did not become ready." >&2
"${compose[@]}" logs --tail=200 api >&2
exit 1
