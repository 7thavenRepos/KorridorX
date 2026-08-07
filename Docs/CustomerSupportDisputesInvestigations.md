# Customer Support, Disputes, Transfer Investigations and SLA

## Scope

KorridorX now maintains customer support tickets, transfer disputes, operational transfer investigations, evidence references, SLA deadlines and escalation state in its own database. Provider support case references can be attached to investigations without making the provider the system of record.

## Customer APIs

- `POST /api/support/tickets`
- `GET /api/support/tickets`
- `GET /api/support/tickets/{ticketId}`
- `POST /api/support/tickets/{ticketId}/messages`
- `POST /api/support/tickets/{ticketId}/evidence`
- `POST /api/support/transfers/{transferId}/disputes`
- `GET /api/support/disputes`
- `POST /api/support/disputes/{disputeId}/withdraw`
- `POST /api/support/disputes/{disputeId}/evidence`

Evidence endpoints record a storage reference and metadata. They do not accept raw files; file-storage upload flows should be layered on top of these records when storage is selected.

## Admin and support APIs

The `Support` role, plus Admin, SuperAdmin and Operations, can use:

- `GET /api/admin/support/tickets`
- `GET /api/admin/support/tickets/{ticketId}`
- `POST /api/admin/support/tickets/{ticketId}/messages`
- `PUT /api/admin/support/tickets/{ticketId}`
- `GET /api/admin/support/disputes`
- `POST /api/admin/support/disputes/{disputeId}/resolve`
- `POST /api/admin/support/transfers/{transferId}/investigations`
- `GET /api/admin/support/investigations`
- `PUT /api/admin/support/investigations/{investigationId}`
- `POST /api/admin/support/investigations/{investigationId}/evidence`
- `GET /api/admin/support/summary`
- `POST /api/admin/support/sla/process`

## Operational holds

Opening a dispute or investigation against a transfer that has not reached a terminal state places an operational hold on the transfer. Payout dispatch checks both compliance holds and operational holds. Resolving or withdrawing the final active dispute/investigation releases the operational hold.

## SLA management

SLA targets are configured under `Support:SlaTargets` by priority. The application stores both first-response and resolution deadlines on every ticket so subsequent configuration changes do not rewrite historical obligations.

The SLA worker is disabled by default. Enable it with:

```text
Support__SlaWorkerEnabled=true
```

Breached tickets are flagged and escalated at most once per 24 hours. If the ticket has an assigned user, an internal notification is queued for that user.

## Migration

Generate a migration after applying this milestone:

```powershell
dotnet ef migrations add AddCustomerSupportDisputesInvestigations

dotnet ef database update
```

The migration should include support tickets, messages, disputes, investigations, evidence and the transfer operational-hold fields.
