# Consumer Google/Apple sign-in — KMOB-017B

This slice adds the Backend contract for signing into an **already-linked Consumer
account** and explicitly linking a provider to an existing Consumer account.
Providers are disabled by default. The installer does not configure a provider,
patch Mobile/native files, install an APK, migrate a database or contact Google/Apple.

Existing Consumer registration stays in the Mobile app; Business registration stays
on the web. A new user first creates/confirms a Consumer account using that flow.
An unlinked provider proof returns `EXTERNAL_ACCOUNT_LINK_REQUIRED`. This slice
does not create accounts or match/merge them by email, including Apple relay email.
KYC, recipients, transfers and wallets continue to belong to the same ApplicationUser.
Social-only signup/onboarding and native UI/provider acceptance remain separate work.

## Contract

All routes are below `/api/auth/external`, use the existing API response envelope,
authentication rate limiting and no-store responses. Requests must use JSON and HTTPS.

| Route | Access | Behavior |
| --- | --- | --- |
| `GET /providers` | Public | Returns provider names and configured Enabled flags. |
| `POST /challenge` | Public | `{provider}` issues a five-minute protected sign-in challenge and nonce. |
| `POST /login` | Public | `{provider,challengeToken,idToken,deviceFingerprint,deviceName}` validates the provider and resolves only its linked stable subject. Returns the existing LoginResultDto, including MFA challenges. |
| `GET /links` | Consumer | Lists provider names only, without provider subjects/email. |
| `POST /links/challenge` | Consumer | `{provider}` issues a link challenge bound to the authenticated Consumer ID. |
| `POST /links` | Consumer | `{provider,challengeToken,idToken,currentPassword,mfaCode,mfaCodeType}` requires current password plus MFA/recovery proof if MFA is enabled. Linking invalidates existing sessions/security stamp and queues an account security notice; the client must sign in again. |

The native client must pass the **exact returned `nonce`** into the provider request.
For Apple the returned value is already a SHA-256 hexadecimal nonce: do not hash it
again. Google also must return this nonce in the ID token; an SDK flow that cannot
bind this nonce does not satisfy the contract and must not be enabled.
Each flow obtains a new challenge and fresh provider token. Cancellation starts a
new flow. ID tokens older than five minutes are rejected. No provider token, current
password, MFA code or challenge token belongs in URLs, analytics or diagnostic logs.

The Backend validates RS256 signature with pinned Google/Apple JWKS, exact issuer,
configured audience/presenter, expiry, issuance time and nonce. Challenges are bound
to provider, purpose and link owner. Duplicate JSON claims and malformed credentials
are rejected. Signed challenge and token hashes are claimed transactionally in the
existing Identity token table, including across API instances. Identity login keys
already enforce unique provider-subject ownership; no migration/new identity table.
The existing MFA, refresh rotation/reuse detection, device/session limits and stamp
checks remain in use. `amr=federated` is retained through MFA and refresh; existing
password sessions retain `amr=pwd`. A privileged/Business role cannot use this channel.

## Configuration and next setup

Configuration section: `ConsumerExternalAuth`. Example structure, kept disabled:

```json
{
  "ConsumerExternalAuth": {
    "Google": { "Enabled": false, "ClientIds": [], "PresenterClientIds": [] },
    "Apple": { "Enabled": false, "ClientIds": [], "PresenterClientIds": [] }
  }
}
```

Do not enable either provider until native configuration, actual ownership of its
client IDs, database workflow acceptance and provider/device acceptance are verified.
Startup rejects an enabled provider without an exact public client-ID allowlist.
`ClientIds` controls token audience; `PresenterClientIds` additionally permits an
explicit `azp` presenter without widening the accepted token audience. No wildcard
IDs, supplied signing-key URLs, guessed client IDs or credentials are accepted.
This JWT proof flow needs public verification keys; Apple web authorization-code
exchange/private key storage and refresh/revocation notification handling remain
follow-up work before enabling Apple on Android/web or declaring Apple complete.

Observed current checkout (read-only KMOB-017A result, 30 September 2026):

- Google Firebase project: `korridorx-staging`, project number `483161506312`.
  Android package: `com.korridorx.korridorx_mobile`. The collected Google services
  configuration has **zero OAuth client entries**; Firebase push setup alone is
  insufficient for sign-in. Obtain the actual debug/release signing fingerprints
  and configure the intended native and server OAuth clients/consent setup next.
- iOS bundle: `com.korridorx.korridorxMobile`. The captured project has no Apple
  sign-in entitlement or Google iOS configuration. Apple App ID/capability,
  provisioning, grouped-app/Services ID needs and permitted audiences must be
  verified in the organization's developer setup before a native patch.
- Current API and Mobile published baselines: Backend `7bffb13a`, Mobile `5e99f987`.
  The next native slice must integrate the existing Consumer AuthController/MFA,
  secure token storage, push registration and accepted five-minute Welcome back.

## Validation and remaining acceptance

The Windows macro runs the real .NET project restore/build, focused
`ConsumerExternal` unit tests (minimum 45 executed), then the full suite with a
minimum of 454 executed cases (the accepted 409-test baseline plus 45 new cases).
This retains the existing password/MFA/session regressions before a Backend-only
commit/push. Counts must match per-case passing evidence.
It clears/restores `KORRIDORX_TEST_CONNECTION_STRING` for these gates because the
existing disposable-database fixture drops its configured public schema.
Database-backed workflow cases therefore remain explicitly skipped/pending.
Only run that fixture against an intentionally disposable localhost database.

Before provider enablement, accept real database link/password/MFA/replay/session
workflows, native challenge/nonce support, cancellation/error handling, verified
client IDs/certificates, actual Google/Apple login and account security behavior.
Include account role/status/lockout/email checks, changed provider email/Apple relay,
an already-owned identity, consent revocation and provider key rotation.
This Backend slice is not full KMOB-017 or production/provider acceptance.

Official contract references:

- https://developers.google.com/identity/gsi/web/guides/verify-google-id-token
- https://developers.google.com/identity/openid-connect/openid-connect
- https://developer.apple.com/documentation/signinwithapple/verifying-a-user

KMOB-031 Wallet/Mobile V2, KMOB-030 push and KMOB-032A screenshot/inactivity acceptance
remain closed. M01, broad D08, live payments/nonzero FX and separately deferred scope
retain their existing pending status.
