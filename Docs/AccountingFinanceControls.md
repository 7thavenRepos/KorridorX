# KorridorX Accounting Ledger and Financial Controls

## Purpose

The accounting module is a finance subledger layered on top of the operational transfer, provider, treasury, and settlement records. It does not replace the business-wallet ledger. Business wallets track customer/business funds; the accounting ledger tracks KorridorX revenue, provider costs, and settlement variances.

## Automated postings

Accounting synchronization is idempotent through `JournalEntry.SourceKey`.

- Completed transfer fee: debit Provider Settlement Clearing, credit Transfer Fee Revenue.
- Positive transfer FX spread: debit Provider Settlement Clearing, credit FX Spread Revenue.
- Provider transaction fee: debit Provider Processing Expense, credit Provider Settlement Clearing.
- Positive settlement variance: debit Provider Settlement Clearing, credit Settlement Variance Gain.
- Negative settlement variance: debit Settlement Variance Expense, credit Provider Settlement Clearing.
- Refunded transfer: reverses fee and FX-spread revenue previously recognized.

The provider-fee posting uses `ProviderTransaction.ProviderFeeAmount` populated from Blaaiz transaction reconciliation (`fee` / `amount_without_fee`).

## Accounting periods

Monthly periods are created automatically when an operational posting is first synchronized. Finance can create custom non-overlapping periods. Closing a period runs accounting sync for that period before locking it. Closed periods reject manual and automated new postings. Only `SuperAdmin` may reopen a closed period.

## Manual journals

Manual journals require at least two lines and must balance to the cent. Each line must contain either a debit or a credit, never both. All lines in a journal use the journal currency.

## APIs

- `GET /api/admin/finance/accounting/accounts`
- `POST /api/admin/finance/accounting/accounts`
- `GET /api/admin/finance/accounting/periods`
- `POST /api/admin/finance/accounting/periods`
- `POST /api/admin/finance/accounting/periods/{id}/close`
- `POST /api/admin/finance/accounting/periods/{id}/reopen`
- `POST /api/admin/finance/accounting/journals/manual`
- `GET /api/admin/finance/accounting/journals`
- `GET /api/admin/finance/accounting/trial-balance`
- `GET /api/admin/finance/accounting/trial-balance.csv`
- `POST /api/admin/finance/accounting/sync`

## Background synchronization

Disabled by default:

```json
"Accounting": {
  "SyncWorkerEnabled": false,
  "SyncIntervalMinutes": 15,
  "LookbackDays": 31
}
```

Enable only after the accounting migration has been applied and finance has reviewed the system account mappings.

## Important limitation

The module reports realized revenue and provider costs by their native currencies. It intentionally does not aggregate different currencies into a single base-currency P&L without an approved accounting FX translation policy.
