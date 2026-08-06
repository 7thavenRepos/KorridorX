# Blaaiz Integration

## Configuration

Set the following under `Blaaiz` in `appsettings.Development.json`, user secrets, or environment variables:

- `IsEnabled`: enables live provider calls.
- `BaseUrl`: sandbox or production API base URL.
- `ClientId` and `ClientSecret`: OAuth client credentials.
- `Scopes`: must include `customer:read`, `customer:write`, and `file:upload` for individual KYC, plus the collection scopes used by the platform.
- `WebhookSigningSecret`: the dedicated webhook signing secret, not the OAuth client secret.
- `WebhookTimestampToleranceMinutes`: replay-window tolerance for signed webhooks.
- `CollectionWalletIds`: collection wallet IDs keyed by source currency.
- `PayoutWalletIds`: payout wallet IDs keyed by the currency KorridorX pays from.
- `AutomaticPayoutDispatchEnabled`: remains `false` until the payout corridor has been validated in sandbox.
- Reconciliation settings control periodic provider transaction status checks.

Do not commit real credentials. Prefer .NET user secrets locally and environment variables in deployed environments.

## Customer and KYC synchronization

Provider customer creation is now orchestrated by the KorridorX KYC flow. The customer-facing provider-sync POST endpoint has been removed to prevent a parallel onboarding path.

Use the endpoints under `/api/kyc` to create the provider customer, request document upload URLs, attach provider file IDs, and submit the application.

`GET /api/customer-profile/me/provider` remains available as a read-only diagnostic endpoint.

## Webhooks

Register both Blaaiz webhook URLs:

- `collection_url`: `POST /api/webhooks/blaaiz/collection`
- `payout_url`: `POST /api/webhooks/blaaiz/payout`

The endpoint validates both `x-blaaiz-signature` and `x-blaaiz-timestamp`, stores each event, rejects replayed or invalid requests, and processes `customer.status_changed` idempotently.

## Collection initiation

Create the local collection first:

`POST /api/transfers/{transferId}/collections`

Then initiate it:

`POST /api/collections/{collectionId}/initiate`

Live provider initiation is blocked until:

- KorridorX `CustomerProfile.KycStatus` is `Approved`.
- The linked `KycProfile.Status` is `Approved`.
- The provider customer status is `VERIFIED`.

Card numbers and CVC values are sent to Blaaiz but are never stored in collection attempts or provider request logs.


## Payouts and reconciliation

Funded transfers can be dispatched to Blaaiz through the payout service. Automatic dispatch is deliberately disabled by default; operations can test individual transfers using `POST /api/admin/transfers/{transferId}/payout/dispatch`.

The reconciliation worker retrieves non-terminal provider transactions and applies collection or payout status changes through the same centralized status workflows used by webhooks. Operations can trigger a batch using `POST /api/admin/providers/blaaiz/reconcile`.
