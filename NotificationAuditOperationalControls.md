# Notification Delivery, Audit Hardening and Operational Controls

## Notification delivery

Notifications remain database-backed outbox records. The delivery worker is disabled by default and must not be enabled until SMTP settings are supplied.

```json
{
  "NotificationDelivery": {
    "WorkerEnabled": false,
    "PollIntervalSeconds": 15,
    "BatchSize": 20,
    "MaxAttempts": 5,
    "RetryBaseMinutes": 2,
    "ProcessingTimeoutMinutes": 10,
    "Smtp": {
      "IsEnabled": false,
      "Host": "",
      "Port": 587,
      "UseSsl": true,
      "Username": "",
      "Password": "",
      "FromAddress": "",
      "FromName": "KorridorX"
    }
  }
}
```

The worker supports Pending, Processing, Retry, Sent and DeadLetter states, exponential retry delays, stale-lock recovery, provider message IDs and manual retry.

Admin endpoints:

```text
GET  /api/admin/notifications?status=DeadLetter&page=1&pageSize=20
POST /api/admin/notifications/{notificationId}/retry
```

## Business context selection

When a user has access to more than one business, business endpoints require:

```text
X-Business-Profile-Id: <business-profile-guid>
```

Available contexts can be retrieved through:

```text
GET /api/business/context
```

Users with exactly one business can continue without the header.

## Wallet operational controls

High-risk wallet credits and manual adjustments require an `Idempotency-Key` header. Reusing the same key with the same request returns the existing result. Reusing it with a different request is rejected.

```text
POST /api/admin/business-wallets/credit
POST /api/admin/business-wallets/{walletId}/adjust
POST /api/admin/business-wallets/{walletId}/freeze
POST /api/admin/business-wallets/{walletId}/unfreeze
POST /api/admin/business-wallets/ledger/{transactionId}/reverse
```

Only standalone funding credits and manual adjustments can be reversed by the generic reversal endpoint. Transfer, batch and collection transactions remain protected by their dedicated recovery workflows.

## Audit logs

Sensitive values are redacted before being written to audit JSON. Audit records include actor, IP address, user agent and correlation ID.

```text
GET /api/admin/audit-logs
```

Supported filters include category, action, entity name, user ID and date range.

## Operational health

```text
GET /api/admin/operations/health
```

The response reports notification backlog, provider failures, webhook failures, stale provider transactions, frozen or inconsistent wallets, active reservations and failed payments.

## Migration and tests

```powershell
dotnet build
dotnet ef migrations add AddNotificationAuditAndOperationalControls
dotnet ef database update
dotnet test
```
