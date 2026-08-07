# Security Hardening, Rate Limits, and Transfer Risk Controls

## API rate limits

KorridorX uses the ASP.NET Core rate-limiting middleware with four configurable controls:

- Global: protects the complete API.
- Authentication: protects registration, login, and refresh-token endpoints by IP address.
- Sensitive: protects money movement, KYC, recipient, wallet, and administrative risk endpoints.
- Webhook: protects public Blaaiz webhook endpoints without bypassing signature verification.

A rejected request returns HTTP 429 with the normal `ApiResponse<T>` error envelope and code `RATE_LIMIT_EXCEEDED`.

## Registration and session security

Public registration permits consumer and business accounts only. Administrative accounts must be provisioned through a controlled administrative workflow and cannot self-register through `/api/auth/register`.

New refresh tokens are stored as SHA-256 hashes rather than raw bearer credentials. Refresh tokens rotate on every refresh. Reuse of a rotated token revokes all active refresh-token sessions for that user.

Users can list and revoke sessions through `/api/auth/sessions`. Revoking a refresh-token session does not retroactively invalidate an already-issued short-lived access token; it prevents further access-token renewal from that session.

## Compliance limits

Active compliance limits are matched by customer type, source country, and source currency. KorridorX checks:

- Per-transfer amount
- Total created during the current UTC day
- Total created during the current UTC month

Pending, funded, processing, completed, and refund-pending transfers count toward limits. Cancelled, rejected, and refunded transfers do not.

## Transfer risk scoring

Risk assessments run when consumer, business, and batch transfers are created. Current rules evaluate:

- Configured high-value thresholds
- Rapid transfer velocity
- Daily transfer count
- Repeated similar transfers to the same recipient
- Recently created recipients or business beneficiaries
- Recent failed login attempts
- Multiple recently used devices
- High-value business transfers that bypass maker-checker approval

Low and medium assessments may continue. High or critical assessments place the transfer on compliance hold. Funding may be received or reserved, but payout submission is blocked until Compliance, Admin, or SuperAdmin releases the hold.

Risk rules are deliberately deterministic and explainable. They should later be supplemented by provider screening, sanctions checks, device intelligence, geolocation, and trained fraud models rather than silently replaced.

## Operational review

Compliance users can list flags, inspect details, reassess a transfer, maintain a hold, or release it. Every review is audited. Operational health now reports open flags, blocking flags, transfers on hold, and failed logins in the last 24 hours.
