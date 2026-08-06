# KorridorX Collection Webhooks, Reconciliation and Payouts

This milestone connects confirmed collection funding to recipient payout processing while keeping KorridorX as the system of record.

## Provider webhook URLs

Register both URLs with Blaaiz:

- Collection URL: `POST /api/webhooks/blaaiz/collection`
- Payout URL: `POST /api/webhooks/blaaiz/payout`

Both endpoints validate `x-blaaiz-signature` and `x-blaaiz-timestamp`, persist the raw event, enforce event idempotency, and record processing attempts.

Collection events update the local `Collection`, its latest attempt, the linked transfer, and the corresponding `ProviderTransaction`. Payout events do the same for `Payout` records and complete the transfer when the recipient payout succeeds.

## Payout dispatch

Payouts are created only after a transfer reaches `PaymentReceived` or `Processing` and the sender passes the central compliance gate.

Currently enabled automatic corridor policies:

- NGN destination: bank transfer using the recipient bank account.
- CAD destination: Interac using the recipient email address.

The source-currency business wallet is resolved from `Blaaiz:PayoutWalletIds`.

Automatic dispatch is disabled by default. Enable it only after sandbox validation:

```json
{
  "Blaaiz": {
    "AutomaticPayoutDispatchEnabled": true
  }
}
```

Operations can dispatch one funded transfer manually:

`POST /api/admin/transfers/{transferId}/payout/dispatch`

## Customer payout endpoints

- `GET /api/payouts?page=1&pageSize=20`
- `GET /api/payouts/{payoutId}`
- `GET /api/transfers/{transferId}/payout`

## Reconciliation

The reconciliation worker periodically retrieves non-terminal provider transactions through the Blaaiz transaction endpoint and applies any status changes through the same collection and payout status services used by webhooks.

Operations can also trigger a batch manually:

`POST /api/admin/providers/blaaiz/reconcile?batchSize=50`

Reconciliation settings:

- `ReconciliationEnabled`
- `ReconciliationIntervalMinutes`
- `ReconciliationBatchSize`

## Status behavior

- Successful collection: transfer moves to `PaymentReceived`.
- Failed or expired collection: the transfer remains available for a supported retry where appropriate.
- Late collection after transfer closure: transfer moves to `RefundPending`.
- Payout initiated or processing: transfer moves through `Processing` and `PayoutInitiated`.
- Successful payout: transfer moves through `PayoutCompleted` to `Completed`.
- Failed or reversed payout: transfer moves to `RefundPending` for operational review.
