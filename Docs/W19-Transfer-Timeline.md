# W19 — transfer timeline presentation

Prepared against 17cd093a03f66a7b354bb51bd550b91c2e745fc5. The two original W18 transfers completed with successful provider responses; the CAD8.55 Interac receipt was confirmed by the user.

The consumer transaction timeline sorted only by OccurredAt. Local processing is recorded before the provider request, but the provider's transaction timestamp can be earlier than that local processing timestamp. A successful response also supplies one OccurredAt for Sending money, Recipient paid and Transfer completed. Timestamp-only sorting therefore displayed Processing above completion and left the equal-time milestones in an unstable order.

The consumer details API now orders timeline events newest first by the immutable time KorridorX recorded each event (CreatedAt), with an explicit milestone tie-breaker only for equal recording times. Original OccurredAt values remain in the response and stored audit rows. No timestamps, histories or financial states are rewritten. Missing legacy CreatedAt falls back to OccurredAt. Separate later retries/failures retain their recording order.

For the supplied screenshot, the final steps appear as Transfer completed, Recipient paid, Sending money, Transfer processing, Earlier delay resolved, Transfer funded, Transfer created.

An earlier PAYOUT_DISPATCH_BLOCKED entry is presented as Earlier delay resolved only when the transfer is currently Completed and a completion event was recorded at or after that delay. Its original ID, event type, metadata and timestamp are preserved. An active or later unresolved delay retains its original title and description. The stored warning remains available for operations/audit review.

The mobile client preserves the API timeline array order and already renders each event title/description, so this correction requires no mobile rebuild. The DTO shape, routes, permissions, monetary balances, source/destination settlement, provider calls, reservation handling and raw audit records are unchanged. No schema or migration.

Three focused regressions cover backdated provider timestamps with audit preservation, equal-time final milestones, and continued visibility of an unresolved warning. Existing CI owns validation/deployment; preparation has not executed .NET or PowerShell locally.
