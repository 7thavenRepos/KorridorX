# KorridorX Release Candidate Validation

This milestone intentionally stops expanding the product surface and focuses on proving that the existing platform can be built, migrated, started, and exercised as connected workflows.

## Automated release gate

Use a **disposable PostgreSQL database**. The RC integration fixture resets the `public` schema before applying migrations.

### PowerShell

```powershell
$env:KORRIDORX_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=korridorx_rc;Username=postgres;Password=<password>"
./scripts/validate-release.ps1
```

### Linux/macOS

```bash
export KORRIDORX_TEST_CONNECTION_STRING='Host=localhost;Port=5432;Database=korridorx_rc;Username=postgres;Password=<password>'
./scripts/validate-release.sh
```

The validation sequence is:

1. clean;
2. restore;
3. release build;
4. apply the complete EF migration chain to a disposable PostgreSQL database;
5. run unit and database-backed integration tests;
6. fail if the EF model contains changes that are not represented by a migration.

The migration-drift gate is important for KorridorX because several milestones add database-backed operational modules. A release candidate should never depend on a developer's existing local schema.

## Database integration tests

Database-backed tests run only when `KORRIDORX_TEST_CONNECTION_STRING` is present. This prevents ordinary unit-test runs from accidentally dropping a developer database.

The configured database **must be disposable**. The fixture executes:

```sql
DROP SCHEMA IF EXISTS public CASCADE;
CREATE SCHEMA public;
```

before applying migrations.

Current connected workflow coverage includes:

- PostgreSQL readiness after migration;
- consumer registration and JWT authentication;
- recipient creation;
- recipient bank-account creation;
- FX quote creation;
- transfer creation from a quote;
- transfer status/timeline creation;
- quote single-use enforcement;
- paged transfer retrieval;
- customer transfer cancellation;
- refresh-token rotation;
- refresh-token reuse detection;
- session revocation after token reuse.

## Provider sandbox release checklist

The automated suite keeps Blaaiz disabled. Before a production release, run the following against the Blaaiz sandbox with controlled test customers and amounts:

- individual customer creation;
- KYC file upload and customer verification;
- business customer creation and KYB submission;
- bank-directory synchronization and account-name resolution;
- card collection initiation where enabled;
- Interac collection initiation;
- collection webhook confirmation;
- NGN bank payout;
- CAD Interac payout;
- payout webhook completion;
- refund initiation for supported corridors;
- transaction reconciliation;
- webhook replay;
- provider wallet synchronization;
- wallet swap/rebalancing;
- settlement statement reconciliation.

Record provider request IDs and KorridorX references for every sandbox test so failures can be traced through provider request logs, webhook events, transfers, collections, payouts, and audit records.

## Compliance release checklist

Before enabling production money movement:

- verify sanctions/PEP provider credentials and licensing;
- validate screening fail-open/fail-closed policy;
- verify transaction-monitoring thresholds by corridor;
- verify compliance limits by customer type/currency;
- test compliance hold and release;
- test SAR/STR maker-checker controls;
- test legal holds and retention dry-runs;
- confirm KYC/KYB gating blocks collection and payout where expected.

## Finance release checklist

Before finance close is treated as authoritative:

- verify system chart-of-account mappings;
- validate provider fee capture against actual provider responses;
- import sample provider invoices and reconcile variances;
- validate settlement statements;
- provide period-average and closing translation rates;
- run accounting synchronization twice and confirm idempotency;
- confirm trial balance remains balanced;
- verify refund revenue reversals;
- complete the finance close checklist using two separate users;
- generate income statement, balance sheet, and close pack.

## Release blockers

A release candidate is blocked if any of the following are true:

- `dotnet build` fails or produces unresolved project warnings;
- any automated test fails;
- migrations cannot build a clean PostgreSQL schema from zero;
- `dotnet ef migrations has-pending-model-changes` reports drift;
- `/health/ready` is unhealthy after migrations;
- provider webhooks cannot be signature-validated;
- a payout can bypass compliance or operational holds;
- accounting journals can become unbalanced;
- required production secrets are absent or placeholders;
- a production provider workflow has not passed sandbox validation.
