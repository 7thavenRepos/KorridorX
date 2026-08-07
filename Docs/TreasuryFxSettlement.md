# Treasury, FX Operations, Provider Liquidity & Settlement

## Provider wallet synchronization

KorridorX stores provider wallet snapshots separately from business customer wallets. The Blaaiz sync calls `GET /api/external/wallet` through the existing OAuth-enabled API client and records wallet ID, currency, active state, balance and synchronization time.

Automatic synchronization is disabled by default. Enable `Treasury:WalletSyncWorkerEnabled` only after provider credentials and the `wallet:read` scope are configured.

## Liquidity thresholds

Operations can define minimum, target and optional maximum balances by provider and currency. The treasury dashboard classifies each provider wallet as Unknown, Critical, Low, Healthy or AboveTarget. Thresholds are monitoring controls and do not automatically move money.

## FX controls

Operations can maintain provider rates and markup rules without changing the customer quote flow. Creating a managed rate deactivates the previous active rate for the same pair. New consumer and business quotes automatically use the current active rate through their existing quote services.

Markup rules reduce the provider rate by the configured percentage and can additionally impose minimum or maximum customer-rate bounds.

Provider rate ingestion is deliberately not automated in this milestone because the current Blaaiz rate documentation uses a different authentication header from the OAuth-protected wallet APIs. Rate entry therefore remains controlled by operations until the account-specific rate authentication contract is confirmed.

## Settlement batches

Settlement batches group successful provider transactions once only, by provider, currency and UTC time window. Collections are inflows; payouts and refunds are outflows. The expected net is calculated as inflows minus outflows.

Reconciliation requires the actual net amount from the provider statement or treasury report. Variances within `Treasury:SettlementVarianceTolerance` are marked Reconciled; larger differences are marked Variance for operational review.

No settlement batch posts accounting entries or moves provider money automatically.
