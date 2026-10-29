# W21 — transaction receipt, report and dispute

## Customer actions

| Transfer status | Actions |
| --- | --- |
| Completed | Download receipt, Dispute |
| Cancelled | Neither Report nor Dispute |
| Other known statuses | Report a problem; retain existing eligible funding/cancellation actions |

Report is a support action, not a replacement financial status.

## API changes prepared

- GET /api/transfers/{id}/receipt: authenticated, owner-only consumer receipt data, restricted to Completed. Decimal strings preserve the original booked values for the mobile PDF. Includes reference, sender/recipient names, sending and recipient amounts, fee, total charge, original exchange rate and separate creation/completion/issue timestamps. Full bank details are excluded; account suffix is masked. Response is no-store.
- POST /api/support/transfers/{id}/reports: description required, up to 2000 characters. Checks ownership and current status on the server. Creates an ordinary Transfer-category support ticket with a live transfer link and a point-in-time message containing transaction details. Reuses an open report ticket; an identical retry adds no duplicate message. A different description is added to the same conversation.
- Existing consumer dispute API now accepts Completed transfers only. It creates the existing dispute and linked support case, including transaction details. Repeated submissions return the customer's active dispute. A supplied support ticket must be open and linked to that same transfer. Business dispute eligibility retains its existing submitted-transfer policy.

Report/dispute creation uses the existing PostgreSQL transaction and a per-transfer row lock to serialize concurrent submissions. Existing closed report cases and resolved/rejected/withdrawn disputes allow a subsequent case.

No report changes transfer status, wallet balances or payout state. A dispute of a completed consumer transfer keeps it completed and does not place an operational hold, create a refund or submit another payout. Existing support roles, evidence uploads, SLA rules and notifications remain in use.

## Mobile integration

The W21 mobile changes retrieve receipt data through the existing authenticated API client, create a branded PDF and use the existing platform file picker to save it. Report and Dispute open the linked existing support conversation and show the case reference. Consumer support's dispute picker lists completed transfers.

Source publication, normal CI/deployment verification and installation of the new signed app build are required before claiming the feature is deployed. The package includes Windows publication and Mac staging Android build scripts.

## Validation

Prepared regression coverage extends the existing destination-wallet settlement fixture to check the authoritative receipt, completed-only dispute, active-case reuse and unchanged funds/provider call count. A separate report workflow checks status restrictions, ownership, transaction details, retry reuse and unchanged funded-transfer balances. No local builds or tests were run; normal CI will compile and run the required checks after the complete package is published.

No schema, migration, provider credential or real environment-file changes.
