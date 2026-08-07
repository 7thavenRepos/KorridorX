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
| `Blaaiz__IsEnabled` | No | Enables live provider calls. |
| `Blaaiz__ClientId` | When enabled | OAuth client ID. |
| `Blaaiz__ClientSecret` | When enabled | OAuth client secret. |
| `Blaaiz__WebhookSigningSecret` | When enabled | Webhook HMAC secret. |
| `NotificationDelivery__WorkerEnabled` | No | Enables email outbox processing. |
| `NotificationDelivery__Smtp__Password` | When SMTP enabled | SMTP password. |

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
