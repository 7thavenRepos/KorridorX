# Regulatory Reporting and Data Retention

## Scope

This module provides an internal SAR/STR preparation and approval workflow. It does not submit reports directly to a regulator. Filing deadlines, report forms, approval requirements, retention periods, and regulator-specific fields must be configured and reviewed for each operating jurisdiction.

## Screening provider

KorridorX supports `ConfiguredWatchlist` for local development and `OpenSanctions` for hosted screening. To enable OpenSanctions:

```text
ComplianceScreening__IsEnabled=true
ComplianceScreening__ProviderCode=OpenSanctions
OpenSanctions__IsEnabled=true
OpenSanctions__ApiKey=<secret>
```

Keep the API key in a secret store or environment variable. Do not commit it to source control.

## Regulatory report lifecycle

```text
Draft -> PendingApproval -> Approved -> Filed
                     \-> Rejected -> Draft
```

The report preparer cannot approve the same report. Filing is recorded only after an approved report is manually submitted to the relevant authority and a filing reference is available.

## Export package

The ZIP export includes:

- regulatory report data
- narrative and suspicion reason
- compliance case metadata
- transfer snapshot where applicable
- screening and AML information
- case notes and evidence manifest
- related audit records

The export is evidence packaging only and is not a regulator-specific filing format.

## Retention safeguards

- No retention policy is seeded automatically.
- The retention worker is disabled by default.
- Policies should initially use `ReviewOnly` and be tested with `dryRun=true`.
- Active legal holds always prevent deletion.
- Open cases and non-terminal reports are never deleted.
- Deletion uses soft-delete semantics.

Enable automatic retention only after legal and compliance approval of jurisdiction-specific retention schedules.
