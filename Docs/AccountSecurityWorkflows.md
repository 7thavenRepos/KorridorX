# Account security workflows

KorridorX implements password recovery, email confirmation, and authenticator-app MFA with
ASP.NET Core Identity and the existing notification outbox. MFA challenge and replay state reuses
the existing `AspNetUserTokens` table, so no database migration is required.

## Public contracts

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/auth/password-reset/request` | Accept an email and always return the same success envelope. |
| `POST` | `/api/auth/password-reset/confirm` | Accept `userId`, encoded token and new password. |
| `POST` | `/api/auth/email-confirmation/request` | Accept an email and always return the same success envelope. |
| `POST` | `/api/auth/email-confirmation/confirm` | Accept `userId` and encoded token. |
| `POST` | `/api/auth/mfa/enrollment/setup` | Return an authenticator URI and manual key for a valid enrollment challenge. |
| `POST` | `/api/auth/mfa/enrollment/confirm` | Confirm enrollment and return the authenticated session plus one-time recovery codes. |
| `POST` | `/api/auth/mfa/challenge/verify` | Complete a login challenge with an authenticator or recovery code. |
| `GET` | `/api/auth/mfa/status` | Return the authenticated account's MFA state and remaining recovery-code count. |
| `POST` | `/api/auth/mfa/recovery-codes/regenerate` | Reauthenticate and replace every recovery code. |
| `POST` | `/api/auth/mfa/reset` | Reauthenticate, disable MFA, revoke sessions, and require enrollment at the next privileged login. |

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
- Business, BusinessAdmin, Compliance, Support, Operations, Admin, and SuperAdmin roles cannot
  receive a session until authenticator enrollment or MFA verification succeeds. Consumer MFA is
  reserved for the mobile authentication phase.
- Password verification returns a five-minute, attempt-limited opaque challenge instead of access
  or refresh tokens. Only the challenge hash is stored.
- Accepted authenticator and recovery codes receive an atomic HMAC replay claim. Authenticator
  codes therefore cannot be reused during their validity window, even across competing challenges;
  recovery codes also remain single-use through ASP.NET Core Identity.
- Recovery codes are displayed only in the successful enrollment or regeneration response.
- MFA access tokens carry `amr=mfa`. JWT validation checks the account's current roles and rejects
  privileged or MFA-enabled sessions that lack this assurance.
- Enrollment and reset update the security stamp and revoke active refresh sessions. Refresh tokens
  created before the enrollment epoch cannot be rotated into an MFA-assured session.
- Recovery-code regeneration and MFA reset require both the current password and a valid
  authenticator or recovery code. Repeated failures participate in Identity lockout.

## Configuration

Development defaults to `RequireConfirmedEmail=false` so local work can continue without SMTP.
Every non-Development, non-Testing environment must set:

```text
Security__Accounts__RequireConfirmedEmail=true
Security__Accounts__FrontendBaseUrl=https://app.example.com
Security__Accounts__TokenLifespanMinutes=120
Security__Mfa__EnforceForPrivilegedRoles=true
Security__Mfa__CodeReplayPepper=<deployment-secret-with-at-least-32-random-characters>
NotificationDelivery__WorkerEnabled=true
NotificationDelivery__Smtp__IsEnabled=true
```

Configure the remaining SMTP host, sender and credential values through the deployment secret
store. Persistent data-protection keys remain mandatory so outstanding links survive safe restarts.
Use a stable deployment secret for `CodeReplayPepper`; changing it does not expose codes, but it
changes the replay-claim namespace during the short retention window.

## Release gate

Run the root `validate-phase2.ps1` in the combined package with a disposable PostgreSQL connection
string. The script performs clean restore/build, migration application, backend tests including the
complete MFA lifecycle, EF model-drift validation, frontend dependency restoration, all Angular
tests, and production/staging builds.
