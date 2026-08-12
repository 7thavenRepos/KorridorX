# KorridorX Environment Variables

ASP.NET Core maps double underscores to configuration sections.

| Variable | Required | Purpose |
|---|---:|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | Use `Production` outside local development. |
| `ConnectionStrings__DefaultConnection` | Yes | PostgreSQL connection string. |
| `Jwt__Issuer` | Yes | JWT issuer. |
| `Jwt__Audience` | Yes | JWT audience. |
| `Jwt__Key` | Yes | Random signing key of at least 32 characters. |
| `AllowedHosts` | Production | Explicit API host names; wildcard is rejected. |
| `Hosting__AllowedOrigins__0` | Production | First permitted browser frontend origin. |
| `Hosting__DataProtectionKeysPath` | Production | Persistent key directory. |
| `Hosting__TrustedProxies__0` | When proxied | Explicit reverse-proxy IP. |
| `Security__Accounts__RequireConfirmedEmail` | Production | Must be `true`; prevents token issuance until email confirmation. |
| `Security__Accounts__FrontendBaseUrl` | Yes | Canonical frontend URL used in confirmation and password-reset links; HTTPS is required in Production. |
| `Security__Accounts__TokenLifespanMinutes` | No | Identity confirmation/reset token lifetime; defaults to 120 minutes. |
| `Security__Mfa__EnforceForPrivilegedRoles` | Production | Must be `true`; requires MFA for business and internal web roles. |
| `Security__Mfa__CodeReplayPepper` | Yes | Random secret of at least 32 characters used to make accepted authenticator codes non-replayable. |
| `Blaaiz__IsEnabled` | No | Enables live provider calls. |
| `Blaaiz__ClientId` | When enabled | OAuth client ID. |
| `Blaaiz__ClientSecret` | When enabled | OAuth client secret. |
| `Blaaiz__WebhookSigningSecret` | When enabled | Webhook HMAC secret. |
| `NotificationDelivery__WorkerEnabled` | No | Enables email outbox processing. |
| `NotificationDelivery__Smtp__IsEnabled` | Production | Must be `true` with the worker so account-security email can be delivered. |
| `NotificationDelivery__Smtp__Password` | When SMTP enabled | SMTP password. |
| `IdentitySeed__SuperAdmin__Email` | First Production startup | Bootstrap Super Administrator email when no Super Administrator exists yet. |
| `IdentitySeed__SuperAdmin__Password` | First Production startup | Bootstrap password supplied only through the deployment secret store. |
| `IdentitySeed__SeedTestUsers` | No | Explicitly enables the complete test identity matrix outside Production; defaults to `false`. |

See `Docs/IdentitySeeding.md` for the complete non-production identity matrix,
local user-secrets helper, first-production-startup behavior, and cleanup steps.

Never commit populated `.env` files, connection strings, JWT keys, OAuth credentials, SMTP passwords, certificates, or database dumps.

## Compliance screening and transaction monitoring

Screening remains disabled until a provider has been configured and validated.

```text
ComplianceScreening__IsEnabled=false
ComplianceScreening__RequireRecentClearScreeningForPayout=false
ComplianceScreening__FailClosedOnProviderError=true
ComplianceScreening__ScreeningValidityDays=30
ComplianceScreening__RescreeningWorkerEnabled=false
ComplianceScreening__RescreeningIntervalHours=24
ComplianceScreening__RescreeningBatchSize=50
```

The built-in `ConfiguredWatchlistScreeningProvider` is intended for development and controlled testing. Production should register a maintained vendor-backed `ISanctionsScreeningProvider`. Production startup rejects an enabled empty configured watchlist.
ComplianceScreening__BlockDeclaredPep=true

## OpenSanctions screening

```text
ComplianceScreening__IsEnabled=true
ComplianceScreening__ProviderCode=OpenSanctions
OpenSanctions__IsEnabled=true
OpenSanctions__ApiKey=<secret>
OpenSanctions__BaseUrl=https://api.opensanctions.org
OpenSanctions__Dataset=default
```

Do not store the API key in source-controlled JSON files. Keep `OpenSanctions__BlockPepMatches=false` unless the approved compliance policy explicitly requires an automatic PEP hold; PEP results should normally enter human review.

## Data retention

```text
DataRetention__WorkerEnabled=false
DataRetention__IntervalHours=24
DataRetention__BatchSize=250
```

Keep the worker disabled until retention policies have been reviewed by legal and compliance teams. Start with `ReviewOnly` policies and dry-run executions.
