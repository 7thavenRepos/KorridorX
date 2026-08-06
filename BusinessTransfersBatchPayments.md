# Business Transfers, Batch Payments and Approvals

This milestone adds business users, permissions, direct business transfers, maker-checker approvals, CSV payment batches and business reporting.

## Approval policy

A business can require one to five approvals. A threshold can limit approvals to transfers or batches at or above a configured total. By default, the creator cannot approve their own request. The API validates that enough eligible approvers exist before a transfer or batch enters `PendingApproval`.

## Funding sources

- `BusinessWallet`: the selected funding source is recorded, but the transfer remains `PendingPayment` until the business-ledger module confirms and reserves sufficient balance.
- `ExternalCollection`: the approved transfer remains `PendingPayment` until a business collection workflow confirms payment.

A funding-source selection never marks a transfer as paid by itself. This prevents payout dispatch before funds are actually confirmed.

## CSV batch format

Required columns:

- `businessBeneficiaryId`
- `destinationCurrencyCode`
- `sourceAmount`
- `purpose`

Optional columns:

- `externalReference`
- `bankAccountId`
- `mobileWalletId`
- `purposeNote`

Exactly one of `bankAccountId` and `mobileWalletId` is required for every row. A maximum of 1,000 data rows is accepted per upload. Use `Docs/business-payment-batch-template.csv` as the starting template.

Each valid row receives its own KorridorX quote. Quotes are refreshed when necessary before approved rows are converted into transfers. Approved batch items remain pending funding; after a later funding confirmation moves each transfer to `PaymentReceived`, the existing payout workflow can dispatch them one at a time while preserving item-level status, retry and reconciliation.

## Important integrity protections

- Quotes and approval counters use optimistic concurrency protection.
- Duplicate approvals from the same user are blocked by unique indexes.
- Business beneficiaries and payout destinations cannot be changed while active transfers reference them.
- Batch destinations are revalidated immediately before transfer materialization.
- Funding confirmation and payout release will require approved business KYB, a verified provider customer and a successful ledger reservation or collection confirmation.
