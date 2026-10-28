# W20 — consumer transfer delivery notifications

Prepared against 1e9981f3608ba3e42dedbef6e45a9810d679cd96.

Consumer completion previously wrote status/history/timeline and staged embedded-business webhooks, but did not queue a sender email or push message. The wallet-funding notice factory does not cover recipient delivery.

The central transfer-status transition now stages a consumer completion receipt in NotificationMessages in the same DbContext/save as the completed transition. This covers immediate success, later webhooks and reconciliation. An accepted or processing payout is not announced as delivered.

The Email queue record supplies the existing in-app inbox and branded email worker. It includes reference, recipient amount, source amount, customer fee and total charge. The generic push notice goes to active registered sender devices when Firebase is enabled. RelatedEntityType/ID remain Transfer/the transfer GUID for existing navigation. Subjects include the reference for operations lookup.

Tracked/stored completion notices are checked for replay. The unchanged status concurrency token and shared commit protect competing completion transitions. The stager performs no SaveChanges or transport call. Existing worker locking, retries, provider IDs and dead-letter handling remain responsible for delivery. The existing status API is synchronous, so contact/device reads occur synchronously in its database-only stager; delivery remains asynchronous.

New completion transitions after deployment receive notices. Deployment does not scan completed history or automatically send old receipts. The W18 payments remain completed; any requested recovery must send receipts only, never replay a payment.

Consumer sender scope only. No notification is addressed to an external payout recipient. No new marketing-preference model, financial mutation, provider retry, environment/credential change, schema or migration. Backend only; no mobile/Angular rebuild.

The full CAD-to-NGN recording-provider regression now also checks one committed receipt and replay deduplication. Focused templates check amounts, encoding and premature-notice rejection. CI owns execution; no local .NET or PowerShell run was performed. SMTP/Firebase acceptance and user receipt remain runtime checks; a queued notice alone does not establish delivery.
