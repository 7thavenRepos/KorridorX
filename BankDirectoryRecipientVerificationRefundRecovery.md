# Bank Directory, Recipient Verification, Refunds and Operational Recovery

## Bank directory

Operations synchronizes provider banks into `ProviderBanks`. Customer-facing recipient forms read from the local table instead of calling Blaaiz directly.

1. Synchronize the required country/currency.
2. List local banks.
3. Save `providerBankId` on the recipient bank account.
4. Verify the account before payout.

Changing bank details clears the existing verification state.

## Payout safety

NGN bank payouts require:

- an active bank account;
- a synchronized provider bank ID;
- successful provider account resolution;
- a provider-resolved account name.

Failed payouts can be retried only by Admin, SuperAdmin, or Operations users. A retry creates a new payout attempt while retaining prior attempts for audit.

## Refund restrictions

The current Blaaiz refund API supports full refunds only for successful EUR or GBP ClearJunction API collections, within seven days, with one refund attempt per collection. KorridorX enforces those provider restrictions before calling the endpoint.

## Operational recovery

Operations users can:

- reconcile one provider transaction;
- request provider webhook replay;
- inspect provider transactions;
- inspect redacted provider request logs;
- inspect webhook processing state;
- retry eligible failed payouts;
- initiate and refresh eligible refunds.


## Recovery status handling

A failed provider refund is recorded as `CollectionStatus.RefundFailed`, distinct from a failed collection attempt. The transfer is routed to an operational failure state when the transfer status permits it, and the provider failure reason remains visible for manual recovery.

Webhook replay is restricted to successful provider transactions within the provider's supported retention window. Inactive bank-directory records are visible only to administrative and operations roles.
