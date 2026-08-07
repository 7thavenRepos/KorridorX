# Sanctions, PEP Screening, Transaction Monitoring and Compliance Cases

## Scope

KorridorX now has a provider-neutral screening and case-management layer for:

- individual customers;
- business customers;
- consumer recipients;
- business beneficiaries;
- business beneficial owners, including declared PEP status;
- transfers and transaction-monitoring alerts.

The default `ConfiguredWatchlistScreeningProvider` is intended for local development, controlled testing, and integration validation. It is not a substitute for a maintained production sanctions/PEP data source. A production deployment should register a vendor-backed implementation of `ISanctionsScreeningProvider`.

## Safe defaults

Screening and scheduled rescreening are disabled by default:

```json
{
  "ComplianceScreening": {
    "IsEnabled": false,
    "RequireRecentClearScreeningForPayout": false,
    "RescreeningWorkerEnabled": false
  }
}
```

Do not enable `RequireRecentClearScreeningForPayout` until a reliable screening provider has been configured and tested. Production startup rejects an enabled empty configured watchlist.

## Screening points

Screening is triggered when:

- an individual customer profile changes;
- an individual KYC application is approved;
- a business profile is created or changed during KYB;
- business KYB is approved;
- a beneficial owner is created or changed;
- a recipient or business beneficiary is created or changed;
- a consumer, business, or batch transfer is created;
- compliance runs a manual screening;
- the rescreening worker finds an expired or missing screening.

Transfer screening checks both the sender and destination party. Blocking results place the transfer on compliance hold before payout.

## Transaction monitoring

The deterministic monitoring engine evaluates:

- rolling 24-hour amount thresholds;
- repeated transfers just below a configured threshold;
- new material destination corridors;
- multiple beneficiaries within a short period.

Review and block thresholds are configurable. A block result creates an AML flag, opens a transaction-monitoring case, and prevents payout.

## Compliance cases

Cases support:

- assignment;
- internal or external-facing notes;
- evidence references and metadata;
- priorities and due dates;
- escalation;
- false-positive, cleared, confirmed-match, report-filed, and no-action decisions;
- audit records for material case actions.

Repeated alerts for the same subject, transfer, and case category are deduplicated into the existing active case while retaining the latest screening record.

## Main administrative endpoints

```text
GET  /api/admin/compliance/cases
GET  /api/admin/compliance/cases/summary
GET  /api/admin/compliance/cases/{caseId}
POST /api/admin/compliance/cases/{caseId}/assign
POST /api/admin/compliance/cases/{caseId}/notes
POST /api/admin/compliance/cases/{caseId}/evidence
POST /api/admin/compliance/cases/{caseId}/decision

GET  /api/admin/compliance/screenings
POST /api/admin/compliance/screenings/run
POST /api/admin/compliance/screenings/rescreen-due
```

Access is restricted to `Admin`, `SuperAdmin`, and `Compliance` roles.

## Configured watchlist example

Use synthetic entries only in source-controlled development settings. Real watchlist data should be loaded through a secure provider integration or protected configuration source.

```json
{
  "ComplianceScreening": {
    "IsEnabled": true,
    "Entries": [
      {
        "Id": "synthetic-test-entry",
        "Name": "Synthetic Test Person",
        "Aliases": ["Test Person"],
        "WatchlistType": 1,
        "ListName": "Development Test List",
        "CountryCode": "NG",
        "IsBlocking": true
      }
    ]
  }
}
```

## Migration

Create and apply a migration after integrating this milestone:

```powershell
dotnet ef migrations add AddSanctionsPepTransactionMonitoringComplianceCases
dotnet ef database update
```
