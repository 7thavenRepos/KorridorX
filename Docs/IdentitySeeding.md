# Environment-safe identity seeding

KorridorX separates startup identity work into role creation, Super Administrator
provisioning, and non-production test-user provisioning.

## Environment behavior

| Environment | Roles | Super Administrator | Test identities |
|---|---:|---:|---:|
| Development | Created idempotently | Created only when configured | Created only when explicitly enabled |
| Staging | Created idempotently | Created only when configured | Disabled by default; may be explicitly enabled |
| Testing | Test fixture owns setup | Not seeded at application startup | Not seeded at application startup |
| Production | Created idempotently | Required on first startup; never modified after one exists | Always blocked, even if the switch is enabled |

Production startup fails safely when the database has no user in the
`SuperAdmin` role and the bootstrap configuration is absent. Once a Super
Administrator exists, the bootstrap values can and should be removed from the
deployment environment. Subsequent startup does not recreate the account or
change its password.

## Configure local Development identities

From the backend repository root in PowerShell, run:

```powershell
.\scripts\configure-development-identities.ps1
```

The helper asks for a local test-user password and a separate Super
Administrator password. It stores the settings through .NET user-secrets,
outside the repository. It creates configuration for these login emails:

| Identity | Development email |
|---|---|
| Consumer | `consumer@dev.korridorx.test` |
| Business owner | `business.owner@dev.korridorx.test` |
| Business administrator | `business.admin@dev.korridorx.test` |
| Compliance | `compliance@dev.korridorx.test` |
| Support | `support@dev.korridorx.test` |
| Operations | `operations@dev.korridorx.test` |
| Administrator | `admin@dev.korridorx.test` |
| Super Administrator | `superadmin@dev.korridorx.test` |

To use a different email domain or country:

```powershell
.\scripts\configure-development-identities.ps1 `
  -EmailDomain "local.example.test" `
  -CountryCode "US" `
  -BusinessName "KorridorX Local Test Business"
```

Start the API normally. The seeder is idempotent, so restarting the application
does not duplicate users, roles, customer profiles, business profiles, or
business memberships. It also never resets an existing account password.

To disable non-production test-user seeding while keeping the created accounts:

```powershell
dotnet user-secrets set "IdentitySeed:SeedTestUsers" "false" --project .\KorridorX.csproj
```

## Production bootstrap

Supply only the following values from the production deployment secret store
for the first startup:

```text
IdentitySeed__SuperAdmin__FirstName
IdentitySeed__SuperAdmin__LastName
IdentitySeed__SuperAdmin__Email
IdentitySeed__SuperAdmin__Password
IdentitySeed__SuperAdmin__PhoneNumber        # optional
IdentitySeed__SuperAdmin__CountryCode        # optional
```

Do not enable `IdentitySeed__SeedTestUsers` in Production. The application has
an unconditional Production guard and will ignore it, but leaving it disabled
keeps the deployment intent clear.

After the first Super Administrator can log in, remove the bootstrap password
and other bootstrap identity values from the production environment. Do not
commit populated settings to `.env`, JSON, scripts, documentation, or source
control.

## Seeded business fixture

When non-production test identities are enabled, the seeder also creates:

- one basic Consumer customer profile;
- one sample business owned by the Business owner;
- one active Owner membership with all business permissions; and
- one active Business administrator membership with all business permissions.

It does not create KYC/KYB approvals, recipients, transactions, balances, or
other scenario data. Those fixtures belong in later, purpose-specific seeders.
