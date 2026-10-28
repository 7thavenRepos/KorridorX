# W16 — funded recipient transfers awaiting Blaaiz payout

Prepared against backend `715e31008a8c0dded6cb5c8c28e88565f78cd124` on 28 October 2026.

The reported transfers are CAD 5 to NGN (`KXTR-20261027-689914`) and NGN 9,000 to CAD (`KXTR-20261028-494848`). Both screenshots show Payment received with a Transfer funded timeline event and no payout event. The older `KXTR-20261021-522082` is Cancelled and must remain excluded.

## Confirmed source defects

1. `PayoutDispatchWorker` requires both Blaaiz IsEnabled and AutomaticPayoutDispatchEnabled. The latter defaults to false. The previous Staging settings and deployment compose file did not set/pass it. The standard deployment therefore leaves recipient payouts waiting for manual dispatch. Enabling WalletWithdrawalProcessing does not enable this worker.
2. PayoutService saves a Pending payout before provider-wallet selection and the live liquidity check. DispatchPendingAsync and the operations queue previously excluded every transfer with any payout row. A preflight failure could create a permanently excluded shell with no provider submission attempt.
3. The per-transfer batch catch discarded exceptions, including failures that happened before there was a durable attempt. API/database health alone did not disclose an inactive dispatch queue.

The screenshots do not prove the effective running-container switch, the presence of payout shells or the precise live reservation balances. Inspect these through operations or the read-only diagnostic script before claiming a case-specific external outcome.

## Resulting behavior

Staging enables automatic recipient dispatch through explicit app settings and compose wiring. `BLAAIZ_AUTOMATIC_PAYOUT_DISPATCH_ENABLED=false` in the existing server-owned `.env.staging` remains an intentional pause; the installer never edits that file. General/Production defaults remain false. `BLAAIZ_ENABLED` must also be true. The worker polls every 30 seconds unless explicitly configured otherwise.

The shared queue includes funded transfers with no payout or a Pending payout without attempts or provider identities. Any attempt, including a deleted one, any provider identity, a deleted payout or a started/terminal payout excludes automatic resubmission. Holds remain enforced. Existing cancellation locks, conditional processing claims, compliance checks, provider wallet selection, live payout liquidity checks and funding capture remain in place.

The worker reuses the existing transfer/reservation. It does not create a replacement transfer or debit the customer a second time. An attempted payout with an uncertain result requires the existing reconciliation/retry/refund process. Cancelled transfers stay out of the queue.

Per-transfer failures emit `KX-PAYOUT-005`, reference, safe ReasonCode and exception type. Raw bank details and provider response bodies are not logged by this change. Scoped tracked changes are cleared between batch items. Operations health now includes `payoutDispatch` settings, eligible/overdue counts and Degraded status for an overdue or disabled funded queue. The existing Pending payouts screen uses the same selection and retains authorized manual dispatch.

## Runtime acceptance

After backend deployment, inspect `GET /api/admin/operations/health` using the existing authorized operations session. Confirm `payoutDispatch.providerEnabled` and `automaticDispatchEnabled`, then find each original reference in Providers & reconciliation / Pending payouts. The two pending payments should advance only if all existing prechecks pass; a provider attempt and response must prove submission. A terminal successful provider state is required to claim settlement.

During a deliberate worker pause, Operations may dispatch the original unsubmitted transfer through the existing authorized action. Never clear attempts or provider IDs, reset balances, or create replacement transfers to force recovery.

On the existing staging server, `bash scripts/deploy/staging-payout-diagnostics.sh` reads the effective non-secret switches, the three reported transfers, payout attempts, reservations and source wallet figures. Its SQL session is read-only. It performs no provider requests, updates, dispatches, restarts, refunds or balance changes. It omits account names/numbers, tokens, connection strings and raw provider payloads.

Support can search `KX-PAYOUT-005` or `Payment received` in Help for the operational explanation. Backend release only; no mobile or Angular rebuild is required for these queue fixes. No schema migration.

## Verification and release state

Source/package checks and shell syntax are checked in the preparation environment. .NET and PowerShell are unavailable there. Run the supplied guarded Windows apply script with `-Build` to compile the API and test project once. Local tests are not executed. Existing CI remains the release gate. Publication, deployment, authenticated runtime acceptance and actual settlement remain pending the returned result.
