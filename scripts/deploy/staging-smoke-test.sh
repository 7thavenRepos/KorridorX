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
check_endpoint "/swagger/index.html"
check_endpoint "/swagger/v1/swagger.json"

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
# KMOB-017D3: this staging release requires Google and keeps Apple disabled.
command -v python3 >/dev/null || { echo "python3 is required for provider JSON validation." >&2; exit 1; }
provider_file="$(mktemp)"
trap 'rm -f "$provider_file"' EXIT
provider_status="$(curl --silent --show-error \
  --output "$provider_file" --write-out '%{http_code}' \
  --connect-timeout 10 --max-time 30 \
  "$base_url/api/auth/external/providers")"
if [[ "$provider_status" != "200" ]]; then
  echo "Provider discovery expected HTTP 200 but returned $provider_status" >&2
  exit 1
fi
python3 - "$provider_file" <<'PY'
import json
import sys

def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate provider response field")
        result[key] = value
    return result

try:
    with open(sys.argv[1], encoding="utf-8-sig") as handle:
        body = json.load(handle, object_pairs_hook=unique_object)
    if not isinstance(body, dict) or body.get("success") is not True:
        raise ValueError("Provider discovery did not return a successful envelope")
    rows = body.get("data")
    if not isinstance(rows, list) or len(rows) != 2:
        raise ValueError("Expected Google and Apple provider records")
    states = {}
    for row in rows:
        if not isinstance(row, dict) or row.get("provider") not in ("google", "apple"):
            raise ValueError("Unexpected provider record")
        provider = row["provider"]
        if provider in states or type(row.get("enabled")) is not bool:
            raise ValueError("Duplicate provider or non-boolean availability")
        states[provider] = row["enabled"]
    if states != {"google": True, "apple": False}:
        raise ValueError("Expected Google enabled and Apple disabled")
except (OSError, UnicodeError, ValueError) as exc:
    print("Google staging verification failed: " + str(exc), file=sys.stderr)
    sys.exit(1)
print("PASS Google enabled; Apple disabled (provider discovery HTTP 200)")
PY
rm -f "$provider_file"
trap - EXIT

echo "KorridorX staging smoke tests passed."
