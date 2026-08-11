#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$script_dir/../.." && pwd)"
cd "$project_dir"

if ! docker image inspect korridorx-api:staging-rollback >/dev/null 2>&1; then
  echo "No korridorx-api:staging-rollback image is available." >&2
  exit 1
fi

export KORRIDORX_IMAGE_TAG=staging-rollback
docker compose --env-file .env.staging -f docker-compose.staging.yml up -d --no-deps --no-build api

echo "Previous API image restored. Database migrations were not reversed."
echo "Confirm /health/live and /health/ready before reopening traffic."
