# Account security workflows

KorridorX implements password recovery and email confirmation with ASP.NET Core Identity token
providers and the existing notification outbox. No database migration is required.

## Public contracts

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/auth/password-reset/request` | Accept an email and always return the same success envelope. |
| `POST` | `/api/auth/password-reset/confirm` | Accept `userId`, encoded token and new password. |
| `POST` | `/api/auth/email-confirmation/request` | Accept an email and always return the same success envelope. |
| `POST` | `/api/auth/email-confirmation/confirm` | Accept `userId` and encoded token. |

All four routes use the authentication rate-limit policy and `Cache-Control: no-store`. Request
responses do not disclose whether an email exists. Identity tokens are Base64URL encoded for API
transport and placed in the email link fragment so web-server and proxy request logs do not receive
them.

## Security behavior

- Deployed environments require confirmed email before registration or login can issue tokens.
- Confirmation and password-reset tokens expire after `Security:Accounts:TokenLifespanMinutes`.
- Confirmation updates the security stamp, making the link single-use.
- Password reset changes the security stamp, revokes every active refresh-token session, clears
  lockout state, and queues a separate security alert.
- JWT validation compares a hashed security-stamp claim with current Identity state. Password
  changes therefore invalidate already-issued access tokens immediately.
- Confirmation and reset email bodies are available only to the SMTP worker. User and administrator
  notification APIs return a redacted body.
- Audit records contain user/action metadata but never tokens or passwords.

## Configuration

Development defaults to `RequireConfirmedEmail=false` so local work can continue without SMTP.
Every non-Development, non-Testing environment must set:

```text
Security__Accounts__RequireConfirmedEmail=true
Security__Accounts__FrontendBaseUrl=https://app.example.com
Security__Accounts__TokenLifespanMinutes=120
NotificationDelivery__WorkerEnabled=true
NotificationDelivery__Smtp__IsEnabled=true
```

Configure the remaining SMTP host, sender and credential values through the deployment secret
store. Persistent data-protection keys remain mandatory so outstanding links survive safe restarts.

## Release gate

Run the root `validate-phase2.ps1` in the combined package with a disposable PostgreSQL connection
string. The script performs clean restore/build, migration application, backend tests, EF model-drift
validation, frontend dependency restoration, all Angular tests, and production/staging builds.

MFA is intentionally not included here. It needs persisted or otherwise replay-safe challenge
state, authenticator enrollment, recovery-code issuance, reset reauthentication, and privileged-role
enforcement as a separate reviewed sub-gate.
