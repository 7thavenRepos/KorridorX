#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$script_dir/../.." && pwd)"
cd "$project_dir"

env_file=".env.staging"
compose_file="docker-compose.staging.yml"
migration_file="${KORRIDORX_MIGRATION_FILE:-artifacts/migrations.sql}"

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

pass() {
  echo "PASS: $*"
}

warn() {
  echo "WARN: $*" >&2
}

[[ -f "$env_file" ]] || fail "Missing $env_file."
[[ -f "$compose_file" ]] || fail "Missing $compose_file."
[[ -f "$migration_file" ]] || fail "Missing reviewed migration artifact: $migration_file"

mode="$(stat -c '%a' "$env_file")"
[[ "$mode" == "600" ]] || fail "$env_file must have mode 600 (current: $mode)."
pass "$env_file permissions are restricted"

get_env() {
  local key="$1"
  local line
  line="$(grep -E "^${key}=" "$env_file" | tail -n 1 || true)"
  printf '%s' "${line#*=}"
}

require_value() {
  local key="$1"
  local value
  value="$(get_env "$key")"

  [[ -n "$value" ]] || fail "$key is required in $env_file."

  case "${value,,}" in
    *replace-with*|*change-me*|*changeme*|*placeholder*)
      fail "$key still contains a placeholder value."
      ;;
  esac
}

require_true() {
  local key="$1"
  local value
  value="$(get_env "$key")"
  [[ "${value,,}" == "true" ]] || fail "$key must be true in Staging."
}

required_values=(
  POSTGRES_DB
  POSTGRES_USER
  POSTGRES_PASSWORD
  JWT_ISSUER
  JWT_AUDIENCE
  JWT_KEY
  ALLOWED_HOSTS
  ALLOWED_ORIGIN
  ACCOUNT_FRONTEND_BASE_URL
  MFA_CODE_REPLAY_PEPPER
  SMTP_HOST
  SMTP_FROM_ADDRESS
)

for key in "${required_values[@]}"; do
  require_value "$key"
done
pass "Required staging values are populated"

require_true REQUIRE_HTTPS_REDIRECTION
require_true REQUIRE_CONFIRMED_EMAIL
require_true MFA_ENFORCE_FOR_PRIVILEGED_ROLES
require_true NOTIFICATION_WORKER_ENABLED
require_true SMTP_ENABLED
pass "Required Staging security/email switches are enabled"

jwt_key="$(get_env JWT_KEY)"
[[ "${#jwt_key}" -ge 64 ]] || fail "JWT_KEY must contain at least 64 characters."

mfa_pepper="$(get_env MFA_CODE_REPLAY_PEPPER)"
[[ "${#mfa_pepper}" -ge 32 ]] || fail "MFA_CODE_REPLAY_PEPPER must contain at least 32 characters."
pass "Deployment-secret minimum lengths are satisfied"

export KX_ALLOWED_ORIGIN="$(get_env ALLOWED_ORIGIN)"
export KX_FRONTEND_BASE="$(get_env ACCOUNT_FRONTEND_BASE_URL)"

python3 - <<'PY'
import os
from urllib.parse import urlparse

def fail(message: str) -> None:
    raise SystemExit(f"FAIL: {message}")

allowed = os.environ["KX_ALLOWED_ORIGIN"].rstrip("/")
frontend = os.environ["KX_FRONTEND_BASE"].rstrip("/")

allowed_uri = urlparse(allowed)
frontend_uri = urlparse(frontend)

for name, parsed in [
    ("ALLOWED_ORIGIN", allowed_uri),
    ("ACCOUNT_FRONTEND_BASE_URL", frontend_uri),
]:
    if parsed.scheme != "https" or not parsed.hostname:
        fail(f"{name} must be an absolute HTTPS URL.")

allowed_origin = f"{allowed_uri.scheme}://{allowed_uri.netloc}"
frontend_origin = f"{frontend_uri.scheme}://{frontend_uri.netloc}"

if allowed_origin.lower() != frontend_origin.lower():
    fail(
        "ACCOUNT_FRONTEND_BASE_URL origin must match ALLOWED_ORIGIN because "
        "StartupConfigurationValidationService requires the frontend origin "
        "to be present in Hosting:AllowedOrigins."
    )

print("PASS: frontend URL and allowed CORS origin are aligned")
PY

unset KX_ALLOWED_ORIGIN KX_FRONTEND_BASE

python3 - <<'PY'
import json
from pathlib import Path

staging_path = Path("appsettings.Staging.json")
production_path = Path("appsettings.Production.json")

staging = json.loads(staging_path.read_text(encoding="utf-8-sig"))
production = json.loads(production_path.read_text(encoding="utf-8-sig"))

staging_swagger = staging.get("Hosting", {}).get("SwaggerEnabled")
production_swagger = production.get("Hosting", {}).get("SwaggerEnabled")

if staging_swagger is not True:
    raise SystemExit(
        "FAIL: Hosting:SwaggerEnabled must be true in appsettings.Staging.json."
    )

if production_swagger is not False:
    raise SystemExit(
        "FAIL: Hosting:SwaggerEnabled must be false in appsettings.Production.json."
    )

print("PASS: Swagger is enabled for Staging and disabled for Production")
PY

command -v docker >/dev/null 2>&1 || fail "docker is not installed."
docker compose version >/dev/null 2>&1 || fail "Docker Compose v2 is not available."

compose=(docker compose --env-file "$env_file" -f "$compose_file")
"${compose[@]}" config --quiet
pass "Docker Compose staging configuration is valid"

branch="$(git branch --show-current 2>/dev/null || true)"
if [[ -n "$branch" && "$branch" != "develop" ]]; then
  warn "Current branch is '$branch'. Existing KorridorX staging deployments track 'develop'."
else
  pass "Git branch is develop (or detached in automation)"
fi

pass "Reviewed migration artifact is present"

echo
echo "KorridorX staging delta preflight: GREEN"
