#!/usr/bin/env bash
set -euo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGPORT:=5432}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"

output_dir="${1:-./backups}"
mkdir -p "$output_dir"

file="$output_dir/korridorx-$(date -u +%Y%m%d-%H%M%S).dump"

pg_dump \
  --format=custom \
  --compress=9 \
  --no-owner \
  --no-privileges \
  --file="$file"

printf 'Backup created: %s\n' "$file"
