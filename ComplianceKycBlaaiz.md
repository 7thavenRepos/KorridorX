# KorridorX Compliance, KYC and Blaaiz Verification

KorridorX owns the customer-facing KYC workflow. Blaaiz remains an internal verification provider.

## Individual KYC flow

1. Start or retrieve the current KYC application.
2. Save identity metadata. KorridorX creates or refreshes the mapped Blaaiz customer.
3. Request a presigned URL for each required document.
4. Upload the binary file directly to the returned URL using the returned headers.
5. Confirm the upload with KorridorX.
6. Submit the KYC application. KorridorX attaches the provider file IDs to the Blaaiz customer.
7. Wait for the signed `customer.status_changed` webhook.
8. KorridorX updates `ProviderCustomer`, `KycProfile`, `KycApplication`, and `CustomerProfile` together.

## Public customer endpoints

- `POST /api/kyc/applications/start`
- `GET /api/kyc/status`
- `PUT /api/kyc/applications/{applicationId}/identity`
- `POST /api/kyc/applications/{applicationId}/documents/upload-url`
- `POST /api/kyc/applications/{applicationId}/documents/{documentId}/confirm`
- `POST /api/kyc/applications/{applicationId}/submit`

## Admin read endpoints

- `GET /api/admin/kyc/applications?status=UnderReview&page=1&pageSize=20`
- `GET /api/admin/kyc/applications/{applicationId}`

## Blaaiz webhook endpoint

- `POST /api/webhooks/blaaiz/collection`

The endpoint validates `x-blaaiz-signature` with the dedicated webhook signing secret and validates `x-blaaiz-timestamp` before processing the event.

## Supported individual identity document types

- `drivers_license`
- `passport`
- `resident_permit`

National identity cards are intentionally rejected by this implementation because they are not supported by the current Blaaiz individual KYC contract.

## Supported document slots

- `IdentityFront`
- `IdentityBack`
- `ProofOfAddress`
- `LivenessCheck`

## Security decisions

- Full identity numbers are not stored in `KycApplication`.
- Only the last four characters and a redacted submission snapshot are retained.
- Presigned upload URLs are returned to the caller but are not persisted.
- Provider request logs redact presigned URLs and identity numbers.
- Collections are gated by approved KorridorX KYC and a `VERIFIED` provider customer.
