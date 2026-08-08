#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
SKIP_DATABASE_TESTS="${SKIP_DATABASE_TESTS:-false}"
SKIP_MIGRATION_DRIFT_CHECK="${SKIP_MIGRATION_DRIFT_CHECK:-false}"

echo "== KorridorX release-candidate validation =="

if [[ "$SKIP_DATABASE_TESTS" != "true" && -z "${KORRIDORX_TEST_CONNECTION_STRING:-}" ]]; then
  echo "KORRIDORX_TEST_CONNECTION_STRING must point to a disposable PostgreSQL database, or set SKIP_DATABASE_TESTS=true." >&2
  exit 1
fi

echo "[1/6] Clean"
dotnet clean KorridorX.slnx --configuration "$CONFIGURATION"

echo "[2/6] Restore"
dotnet restore KorridorX.slnx

echo "[3/6] Build"
dotnet build KorridorX.slnx --configuration "$CONFIGURATION" --no-restore

if [[ "$SKIP_DATABASE_TESTS" != "true" ]]; then
  echo "[4/6] Apply migrations to disposable RC database"
  export ConnectionStrings__DefaultConnection="$KORRIDORX_TEST_CONNECTION_STRING"
  dotnet ef database update --no-build --configuration "$CONFIGURATION"
else
  echo "[4/6] Database migration application skipped"
fi

echo "[5/6] Tests"
dotnet test KorridorX.slnx \
  --configuration "$CONFIGURATION" \
  --no-build \
  --logger "trx;LogFileName=release-candidate-tests.trx" \
  --results-directory artifacts/release-candidate

if [[ "$SKIP_MIGRATION_DRIFT_CHECK" != "true" ]]; then
  echo "[6/6] Pending EF model-change gate"
  dotnet ef migrations has-pending-model-changes --no-build --configuration "$CONFIGURATION"
else
  echo "[6/6] Pending EF model-change gate skipped"
fi

echo "Release-candidate validation completed successfully."
