#!/usr/bin/env bash
# Read-only evidence for the three reported recipient transfers. No provider
# requests, database updates, restarts, dispatches, refunds or balance changes.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$script_dir/../.." && pwd)"
cd "$project_dir"
[[ -f .env.staging ]] || { echo 'Missing server-owned .env.staging.' >&2; exit 1; }
compose=(docker compose --env-file .env.staging -f docker-compose.staging.yml)
api_id="$("${compose[@]}" ps -q api)"
[[ -n "$api_id" ]] || { echo 'Staging API container is not running.' >&2; exit 1; }
echo 'READ_ONLY_PAYOUT_DIAGNOSTICS'
date -u '+CheckedAt=%Y-%m-%dT%H:%M:%SZ'
echo "SourceCommit=$(git rev-parse HEAD)"

# Whitelist four non-secret environment settings. Never print docker inspect
# or a compose configuration containing connection strings/credentials.
docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$api_id" |
  awk -F= '$1 == "ASPNETCORE_ENVIRONMENT" || $1 == "Blaaiz__IsEnabled" ||
    $1 == "Blaaiz__AutomaticPayoutDispatchEnabled" ||
    $1 == "Blaaiz__PayoutDispatchIntervalSeconds" {print}'
docker exec "$api_id" cat /app/appsettings.Staging.json |
  python3 -c 'import json,sys; b=json.load(sys.stdin).get("Blaaiz",{}); print("ImageStagingAutomaticPayoutDispatchEnabled="+str(b.get("AutomaticPayoutDispatchEnabled","UNSET; options default false")).lower()); print("ImageStagingPayoutDispatchIntervalSeconds="+str(b.get("PayoutDispatchIntervalSeconds","UNSET; options default 30")))'

"${compose[@]}" exec -T postgres sh -c \
  'PGPASSWORD="$POSTGRES_PASSWORD" PGOPTIONS="-c default_transaction_read_only=on -c statement_timeout=15000" psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -X -A -t --set ON_ERROR_STOP=1' <<'SQL'
BEGIN READ ONLY;
WITH affected AS (
  SELECT * FROM "Transfers" WHERE "Reference" IN (
    'KXTR-20261027-689914','KXTR-20261028-494848','KXTR-20261021-522082'
  )
)
SELECT jsonb_build_object(
  'reference', t."Reference", 'transferId', t."Id", 'statusRaw', t."Status",
  'sourceCurrency', t."SourceCurrencyCode", 'sourceAmount', t."SourceAmount",
  'totalPayable', t."TotalPayableAmount", 'destinationCurrency', t."DestinationCurrencyCode",
  'destinationAmount', t."DestinationAmount", 'providerTransferId', t."ProviderTransferId",
  'providerReference', t."ProviderReference", 'paymentReceivedAt', t."PaymentReceivedAt",
  'complianceHold', t."IsComplianceHold", 'operationalHold', t."IsOperationalHold",
  'payouts', COALESCE((SELECT jsonb_agg(jsonb_build_object(
    'id', p."Id", 'reference', p."Reference", 'statusRaw', p."Status", 'isDeleted', p."IsDeleted",
    'providerPayoutId', p."ProviderPayoutId", 'providerReference', p."ProviderReference",
    'attempts', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'id', a."Id", 'statusRaw', a."Status", 'attemptedAt', a."AttemptedAt",
      'isDeleted', a."IsDeleted", 'providerRequestId', a."ProviderRequestId",
      'providerResponseId', a."ProviderResponseId"
    )) FROM "PayoutAttempts" a WHERE a."PayoutId" = p."Id"), '[]'::jsonb)
  )) FROM "Payouts" p WHERE p."TransferId" = t."Id"), '[]'::jsonb),
  'reservations', COALESCE((SELECT jsonb_agg(jsonb_build_object(
    'id', r."Id", 'statusRaw', r."Status", 'amount', r."Amount",
    'captured', r."CapturedAmount", 'released', r."ReleasedAmount",
    'remaining', r."Amount" - r."CapturedAmount" - r."ReleasedAmount",
    'isDeleted', r."IsDeleted"
  )) FROM "FinancialReservations" r WHERE r."RelatedEntityType" = 'Transfer'
     AND r."RelatedEntityId" = t."Id"), '[]'::jsonb),
  'sourceWallet', (SELECT jsonb_build_object(
    'currency', f."AssetCode", 'settled', f."SettledBalance", 'available', f."AvailableBalance",
    'held', f."HeldBalance"
  ) FROM "FinancialAccounts" f WHERE f."Id" = t."SourceFinancialAccountId"),
  'providerRequests', COALESCE((SELECT jsonb_agg(jsonb_build_object(
    'id', l."Id", 'endpoint', l."Endpoint", 'method', l."HttpMethod", 'statusRaw', l."Status",
    'httpStatus', l."ResponseStatusCode", 'requestedAt', l."RequestedAt"
  )) FROM "ProviderRequestLogs" l WHERE l."RelatedTransferId" = t."Id"
    OR l."RelatedPayoutId" IN (SELECT p."Id" FROM "Payouts" p WHERE p."TransferId" = t."Id")), '[]'::jsonb)
) FROM affected t ORDER BY t."Reference";
ROLLBACK;
SQL
echo 'TransferStatusLegend=4:PaymentReceived,5:Processing,6:PayoutInitiated,8:Completed,9:Failed,10:Cancelled,11:RefundPending,12:Refunded'
echo 'PayoutStatusLegend=1:Pending,2:Initiated,3:Processing,4:Successful,5:Failed,6:Reversed'
echo 'READ_ONLY_PAYOUT_DIAGNOSTICS_COMPLETE'
