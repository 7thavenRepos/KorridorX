# Blaaiz Core Integration

## Configuration

Set the following under `Blaaiz` in `appsettings.Development.json` or user secrets:

- `IsEnabled`: enables live provider calls.
- `BaseUrl`: use `https://api-dev.blaaiz.com` for sandbox or `https://api-prod.blaaiz.com` for production.
- `ClientId` and `ClientSecret`: OAuth client credentials.
- `Scopes`: space-separated OAuth scopes assigned to the credentials.
- `CollectionWalletIds`: business wallet IDs keyed by currency. A wallet ID is required for API card collections.

Do not commit real credentials. Prefer .NET user secrets locally and environment variables in deployed environments.

## Customer synchronization

`POST /api/customer-profile/me/provider/sync`

For a new individual provider customer, supply `idType`, `idNumber`, `idIssueDate`, and `idExpiryDate`. Once a provider customer already exists, send an empty object to refresh its status from Blaaiz.

KorridorX masks identity numbers in provider request and response logs.

## Collection initiation

Create the local collection first:

`POST /api/transfers/{transferId}/collections`

Then initiate it:

`POST /api/collections/{collectionId}/initiate`

Live initiation currently supports:

- `Card`: requires a configured currency wallet, card details, and a provider customer whose status is `VERIFIED`.
- `Interac`: requires a CAD collection and payer email. Settlement remains asynchronous until webhook confirmation.

Card numbers and CVC values are sent to Blaaiz but are never stored in collection attempts or provider request logs.

## Database migration

This milestone adds `Collection.ProviderExpiresAt` and a unique provider/customer-profile index. Create and apply a migration locally.
