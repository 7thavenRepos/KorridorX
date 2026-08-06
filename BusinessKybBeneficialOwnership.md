# Business KYB, Beneficial Ownership and Business Beneficiaries

This milestone adds the business onboarding and beneficiary foundation to KorridorX while keeping Blaaiz behind the compliance and provider abstractions.

## Business KYB flow

1. An authenticated user starts a business KYB application.
2. KorridorX creates or reuses the user's `BusinessProfile` and active `BusinessKybApplication`.
3. KorridorX creates or updates the matching Blaaiz `type=business` customer.
4. For FULL KYB, the user adds beneficial owners whose ownership percentages total exactly 100%.
5. The frontend requests an upload URL for each owner's identity-document side, uploads the file directly to the returned URL using the returned headers, and confirms the provider file ID.
6. The frontend requests upload URLs for business formation and supporting documents, uploads each file directly, and confirms registration.
7. KorridorX validates submission readiness and submits the provider customer for review.
8. The existing signed `customer.status_changed` webhook synchronizes business, owner and document statuses into KorridorX.
9. Rejected owners and documents retain provider comments so the customer can correct and resubmit them.

The provider presigned URL is temporary and is never persisted. KorridorX stores only provider file/document IDs and the local verification state.

## FULL and MINIMAL KYB

`BusinessKybScope.Full` is the default and requires beneficial owners, owner identity files and at least one formation document.

`BusinessKybScope.Minimal` is disabled by default. It may be enabled only after the Blaaiz account has been approved for MINIMAL KYB:

```json
{
  "Blaaiz": {
    "MinimalBusinessKybEnabled": true
  }
}
```

MINIMAL KYB requires the business identity and a formation document but does not require beneficial owners. Do not enable it merely to bypass the FULL readiness checks.

## Security decisions

- Owner identity numbers are encrypted with ASP.NET Core Data Protection.
- Only the last four characters are returned in API responses.
- Provider request logs mask owner identity numbers and tax identifiers.
- Presigned upload URLs are redacted from provider response logs.
- Approved and under-review KYB records are locked against normal customer edits.
- Verified-business corrections remain an operational process rather than an unrestricted API update.

## Business beneficiary flow

Business beneficiaries are separate from consumer recipients and are always owned by a `BusinessProfile`.

A business can:

- Create individual or business beneficiaries.
- Add bank accounts and mobile wallets.
- Select synchronized provider banks.
- Verify bank-account ownership through the provider account-resolution API.
- Set default payout destinations.
- Soft-delete beneficiaries and payout destinations.

The next business-transfer milestone will use these records as immutable payout snapshots and will enforce approved business KYB before funding or provider submission.

## Migration

```powershell
dotnet ef migrations add AddBusinessKybBeneficialOwnershipAndBusinessBeneficiaries
dotnet ef database update
```
