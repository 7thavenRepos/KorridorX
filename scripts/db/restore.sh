#!/usr/bin/env bash
set -euo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGPORT:=5432}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"

backup_file="${1:?Usage: restore.sh <backup-file> --confirm}"
confirmation="${2:-}"

if [[ "$confirmation" != "--confirm" ]]; then
  echo "Restore is destructive. Re-run with --confirm after verifying the target database." >&2
  exit 1
fi

if [[ ! -f "$backup_file" ]]; then
  echo "Backup file not found: $backup_file" >&2
  exit 1
fi

pg_restore \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  --dbname="$PGDATABASE" \
  "$backup_file"

printf 'Restore completed from: %s\n' "$backup_file"
