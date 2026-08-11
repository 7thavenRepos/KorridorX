#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-}"
if [[ -z "$base_url" ]]; then
  echo "Usage: $0 https://api.staging.korridorx.com" >&2
  exit 2
fi

base_url="${base_url%/}"

check_endpoint() {
  local path="$1"
  local response_file
  response_file="$(mktemp)"
  trap 'rm -f "$response_file"' RETURN

  local status
  status="$(curl --silent --show-error --location \
    --output "$response_file" \
    --write-out '%{http_code}' \
    --connect-timeout 10 \
    --max-time 30 \
    "$base_url$path")"

  if [[ "$status" != "200" ]]; then
    echo "$path returned HTTP $status" >&2
    sed -n '1,40p' "$response_file" >&2
    exit 1
  fi

  echo "PASS $path (HTTP 200)"
  rm -f "$response_file"
  trap - RETURN
}

check_endpoint "/health/live"
check_endpoint "/health/ready"

unauthorized_status="$(curl --silent --show-error \
  --output /dev/null \
  --write-out '%{http_code}' \
  --connect-timeout 10 \
  --max-time 30 \
  "$base_url/api/admin/operations/health")"

if [[ "$unauthorized_status" != "401" ]]; then
  echo "Protected endpoint expected HTTP 401 but returned $unauthorized_status" >&2
  exit 1
fi

echo "PASS protected endpoint rejects anonymous access (HTTP 401)"
echo "KorridorX staging smoke tests passed."
