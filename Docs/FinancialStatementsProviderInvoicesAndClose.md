# Financial Statements, Provider Invoices, Tax and Finance Close

This milestone extends the KorridorX accounting subledger with management financial statements, provider invoice reconciliation, tax configuration, and maker-checker period close.

## Financial statements

`GET /api/admin/finance/statements/income?from=...&to=...`

`GET /api/admin/finance/statements/balance-sheet?asOf=...`

Native-currency balances remain authoritative. Optional base-currency totals use `FinanceTranslationRate` records. Income statements use `PeriodAverage` rates and balance sheets use `Closing` rates. Missing rates are surfaced rather than silently treated as 1.

## Provider invoices

Import provider invoice CSV files at `POST /api/admin/finance/provider-invoices/import`. Required CSV columns are `description`, `net_amount`, `tax_amount`, and `total_amount`. `provider_transaction_id` and `provider_reference` are optional matching keys.

Provider invoices are matched against captured provider fees. A different finance/operations user must approve an imported invoice. Approved variances and provider tax amounts are posted through the accounting subledger using immutable journal entries.

## Tax rules

Tax rules are configuration only until explicitly used by a KorridorX flow. The tax calculator supports inclusive and exclusive VAT/GST/sales-tax style calculations and does not silently alter customer pricing.

## Finance close

Each accounting period gets a close checklist. System items verify accounting synchronization, trial-balance balance, provider invoice review, and settlement variance resolution. Tax and management reviews are manual checklist items.

A close request cannot be submitted until all required items are complete. The requester cannot approve their own close request. Approval closes the accounting period. The older direct period-close endpoint remains available only to `SuperAdmin` as an emergency control.

## Accruals

`POST /api/admin/finance/accruals` posts an expense/liability accrual and can optionally schedule an exact reversing journal in a later open accounting period. Accruals use immutable journal entries and are included automatically in financial statements.
