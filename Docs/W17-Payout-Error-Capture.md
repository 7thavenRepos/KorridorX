# W17 — payout and Blaaiz error capture

Prepared against 961584e152c7808912b3b501c26e2ca6256f5e4c.

The 28 October staging snapshot confirms the recipient worker is enabled and polling every 30 seconds. CAD transfer KXTR-20261027-689914 has an active C$6 reservation; NGN transfer KXTR-20261028-494848 has an active NGN9,100.90 reservation. Nothing was captured. Neither has a payout, provider attempt or provider request recorded. Both fail an internal precheck with InvalidOperationException. This evidence does not establish the exact precheck; no provider rejection has been observed for either payment.

This release records the exact failure stage and redacted reason, plus HTTP status, provider error code, provider request reference and internal provider-request-log ID where available. A persistent, deduplicated pre-submission timeline event gives customers a support code and concise explanation. It does not change transfer status, release/capture reservations, retry attempted payouts or bypass any compliance/recipient/channel/liquidity guard.

Provider error JSON is parsed safely when error/message fields are objects, arrays, nulls or malformed. The original HTTP status and audited response remain available. Authentication failure is linked to the intended operation, while OAuth response bodies and credentials are not copied into transfer diagnostics. Normal tokens, configured scopes and payment payloads are unchanged. Domain payout rejections use the provider response explanation when present.

Provider request logs can be searched by transfer or payout reference through the existing operations screen. Error text includes the provider error code and request reference when supplied. The existing KX-PAYOUT-005 help article is extended; no extra catalog entries or new schema migrations.

After deployment, run the existing read-only staging-payout-diagnostics.sh command. It now includes recorded precheck diagnostics and provider error messages. The first worker pass should identify the current gate for each original transfer. The current settlement issue is not claimed resolved by adding diagnostics; use that recorded reason to apply the appropriate correction or configuration/compliance action. Never submit replacement transfers merely to collect an error.

The supplied guarded installer applies and publishes only reviewed backend files. Existing CI performs compilation/checks and staging deployment; no local build/test loop is required. Installation makes no provider calls or direct database/financial changes. Existing background workers continue under the existing execution rules. Runtime acceptance and individual provider outcomes remain pending.
