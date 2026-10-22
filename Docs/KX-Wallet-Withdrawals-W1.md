# KorridorX Same-Currency Wallet Withdrawals — W1 (safe destination foundation)

**Scope:** Wallet cash-outs, same currency only. Cross-currency transfers belong to **Send**.

This release does NOT call a payout provider, debit, reserve, or credit a wallet, calculate final fees, or expose a Withdraw confirmation button. It introduces wallet-linked destination setup for personal and business accounts; no live movement occurs until later stages are validated.

## Implemented methods

- **NGN**: Nigerian bank transfer, after current provider account-name verification matches the wallet owner's verified name. Stored bank account number uses ASP.NET Core Data Protection with the deployment's existing persisted keys, and is never included in API responses, security email or push.
- **CAD**: Interac using the confirmed email of the personal user or business owner. Other emails require the existing Send workflow.
- **USD, GBP, EUR**: not yet implemented in the KorridorX payout adapter. They remain unavailable until their destination fields, provider prerequisites, authorization and settlement processing are implemented and verified. Do not advertise them as ready.

Eligibility relies on the configured and activated Admin Payment Channels, asset/owner settings, wallet ownership, business `ManageFunding` permissions, and existing compliance gates. Bank setup requires the consumer's transaction PIN. Business staff need the selected-business permission and preexisting account security.

## Initial APIs

Personal: `GET /api/wallets/{walletId}/withdrawal-methods`; `GET /api/wallets/{walletId}/withdrawal-destinations`; `POST /api/wallets/{walletId}/withdrawal-destinations`; `POST /api/wallets/{walletId}/withdrawal-destinations/{destinationId}/disable`.

Business: the same suffixes under `/api/business/funding/wallets/{walletId}`; uses the selected business context.

POST body: `{"paymentMethod":1,"providerBankId":"...","accountNumber":"0123456789","transactionPin":"...."}` for NGN consumer bank (the bank ID comes from the configured provider directory); `{"paymentMethod":3,"email":"confirmed@example.com","transactionPin":"...."}` for CAD consumer. Business does not use consumer PIN.

Every verified addition/removal records an audit and queues an account security email/push. Saved destination reads return only masked account digits, not plaintext. Linking cannot cause money movement.

## Required before W2/W3 go live

1. Implement authoritative withdrawal fee preview from provider fees breakdown; choose `from_amount` inclusive mode, showing both net receipt and total wallet debit, and fail closed if the provider fee cannot be obtained.
2. Create idempotent withdrawal intents with source-wallet holds, business approvals where configured, PIN/MFA/risk/limits, provider dispatch with unique merchant reference, and fail-closed timeout/unknown-status handling.
3. Verify provider payout status/webhook + amount/currency/wallet/destination, capture exactly once on success, release on verified terminal failure, and queue user notifications transactionally.
4. Add individual Flutter and business Angular forms, history and statuses, plus admin reconciliation. Never allow cross-currency methods here; use Send.
5. Validate actual Blaaiz payout-channel capability and permissions and conduct controlled low-value live acceptance only after all guards pass.
