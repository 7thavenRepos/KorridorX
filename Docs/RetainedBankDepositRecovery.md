# Recover a retained bank deposit

Use this workflow when Blaaiz confirmed a dedicated-bank-account deposit, the wallet has not been credited, and the original webhook is retained as Ignored or Failed. Blaaiz does not need to resend the event. This is available to Admin, SuperAdmin and Operations through **Providers → Webhooks → Inspect**.

## Review and recover

1. Locate the original webhook using its provider event ID, status and time. Inspect its transaction ID, currency, amount and receiving account. A later deposit from the same sender is a separate transaction and does not replace the original.
2. Enter a meaningful audit reason, then choose **Verify retained bank deposit**. The server reads the retained event, checks its authenticity and current provider connection, and independently reads the transaction from Blaaiz. Verification does not credit a wallet.
3. Review the verified transaction ID, amount and currency, receiving account, destination wallet and whether it is already credited. The wallet and amount come from verified records; they cannot be entered or reassigned by the operator.
4. Choose **Recover verified deposit** to credit the verified original deposit. The server verifies again and uses the existing bank-deposit settlement path. If the transaction is already credited, the action records the verified result without another credit.
5. Inspect the updated event and processing history. Confirm the collection and wallet credit in the financial records. Funding notifications are queued through the usual email and push delivery service; inspect the notification queue if delivery needs investigation.

The preview expires. If it expires, the selected event changes, or account/provider verification changes, verify again before applying. A failed request does not justify sending another payment or using a manual balance adjustment.

## What the operation records

The original payload, event identity, received time and signature headers remain unchanged. Recovery adds a processing attempt and an audit record with the operator and reason. Successful settlement creates or recognizes the normal collection, provider-transaction receipt, provider-credit claim and ledger credit. The successful recovery receipt and audit commit with the financial result.

The same provider transaction cannot be credited twice through repeated recovery, simultaneous recovery, or a later provider webhook. An independently verified deposit with a different provider transaction ID can still be credited normally.

## Eligibility and blocked results

- The retained event must describe a successful dedicated-bank collection with an explicit receiving-account ID. Source-bank details, sender name and customer email are not receiving-wallet ownership keys.
- The receiving account must belong to the current application environment and Blaaiz connection. Its exact customer/account identity, amount, currency and transaction status must agree with provider verification.
- The receiving wallet must be active and its owner must meet the existing KYC/KYB checks. A paused channel does not discard valid deposits into accounts already issued; wallet/compliance restrictions still apply.
- Payouts, Interac Money Requests, unsolicited Interac matching and other payment flows retain their existing controls. This action does not send money, request a new payment or call provider webhook replay.

If a preview or recovery is rejected, keep the event and investigate the displayed reason. Do not edit its payload, substitute another destination wallet, fabricate a signature or insert a balance adjustment. An unavailable provider verification endpoint must be resolved before recovery can proceed. If credentials or the webhook signing secret changed, account/signature verification may require operations investigation; the recovery flow does not bypass connection isolation.

## API

Authenticated `Admin`, `SuperAdmin` and `Operations` roles use:

| Operation | Endpoint | Body |
|---|---|---|
| Verify and preview | `POST /api/admin/providers/webhooks/{localWebhookEventId}/bank-deposit-recovery/preview` | `{ "reason": "Reason for recovering this retained deposit" }` |
| Apply the reviewed recovery | `POST /api/admin/providers/webhooks/{localWebhookEventId}/bank-deposit-recovery/apply` | `{ "previewToken": "opaque token returned by preview" }` |

The URL uses the local webhook row ID shown by the admin application, not the provider event ID or provider transaction ID. The protected preview is bound to the operator, reason, retained payload and verified destination. Do not log or share preview tokens.

## Release boundary

The implementation release applies backend and web code only. It does not recover deposits automatically, contact payment endpoints or change balances. The explicit admin recovery after deployment is the financial action. Mobile changes and a new database migration are not required.
