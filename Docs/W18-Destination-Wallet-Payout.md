# W18 — destination-wallet recipient settlement

Prepared against backend 4c8cecf3661c225d76cc9c7a5f4f3a57cd37fea1.

## Correct execution contract

| Customer sending wallet | Recipient receives | Blaaiz payout wallet | Provider currency pair |
|---|---|---|---|
| CAD | NGN | NGN | NGN to NGN |
| NGN | CAD | CAD | CAD to CAD |
| Any supported source | USD / GBP / EUR | Matching USD / GBP / EUR | Receiving currency to itself |

The customer funds the transfer internally in the source currency. The existing reservation is captured under the existing successful-payout rule. Blaaiz pays the agreed DestinationAmount using to_amount from the receiving-currency wallet, with from_currency_id and to_currency_id both set to that currency. The quote, source amount, customer fee and agreed conversion are preserved. There is no additional provider FX conversion and no second customer debit.

## Confirmed failure and correction

The 28 October 21:47:19 UTC runtime snapshot recorded payment_channel failures for KXTR-20261027-689914 and KXTR-20261028-494848, with no payouts, attempts or provider requests. Their CAD6 and NGN9,100.90 reservations were active and uncaptured. The earlier cancelled transfer remained cancelled with its CAD6 reservation fully released.

The returned Admin routes already use an NGN wallet for NGN bank payouts and a CAD wallet for CAD Interac. Those are correct for the agreed settlement model. The backend incorrectly required the source currency in channel and wallet selection and would also send a cross-currency provider request. W18 aligns channel selection, provider wallet selection, exact selected-wallet liquidity verification and provider payout currencies with the receiving currency. The shared Blaaiz currency requirements own that settlement-wallet rule.

No Admin route change is needed for the two returned channels. The earlier proposed CAD-funded NGN and NGN-funded CAD route table was incorrect and is superseded. Mismatched provider-wallet/channel currencies are now reported as not ready and cannot be activated. Existing saved payout selections are never silently retargeted: incompatible historical selections require operations reconciliation. The two reported pending transfers have no such selections in the supplied snapshot and can use their existing reservations on the next eligible worker cycle after deployment.

Consumer creation previously checked channel availability only for USD/GBP/EUR recipients. It now uses the same eligibility check for managed CAD/NGN too, before PIN verification, quote consumption or wallet reservation. Foreign bank recipients retain their explicit-channel requirement. Never-managed CAD/NGN legacy scopes retain their established behavior; pausing managed routes does not restore fallback. Dispatch rechecks configuration, recipient readiness, compliance, holds, liquidity and duplicate-submission rules.

Provider error/status/reference capture and KX-PAYOUT-005 diagnostics remain available. Knowledge-base articles describe the corrected flow and exact error. No catalog entries are added. No changes to customer funding/capture/release services, fees, rates, house funding, credentials, environment files, schema or migrations. Backend only; no mobile or Angular rebuild.

## Validation and rollout

The guarded Windows script backs up and publishes only the ten reviewed backend paths. Existing CI owns compilation, regression checks and deployment; no local build/test loop. This preparation environment has neither .NET nor PowerShell, so execution is pending returned publication evidence and CI.

Focused regressions cover both destination-wallet channel directions, invalid source-currency wallet activation, managed-route rejection before reservation, and a CAD customer transfer settled once through the NGN provider wallet using recording adapters and the existing CI database fixture. They make no real provider requests.

After deployment, run the existing read-only staging-payout-diagnostics.sh command. Inspect the original references for provider IDs, latest failure stage, attempts and confirmed reservation outcome. Successful source publication or deployment alone does not establish recipient settlement. No replacement payments, forced retries, balance edits or manual provider calls are part of this installer.

## Provider contract reference

Official Blaaiz payout and money-movement documentation checked 28 October 2026: https://docs.blaaiz.com/docs/payments/payout/create-a-payout and https://docs.blaaiz.com/docs/guides/money-movement . The NGN example uses an NGN-to-NGN payout. to_amount fixes the recipient amount; applicable provider fees are added to the provider-wallet debit under the existing provider contract.
