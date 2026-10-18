# KorridorX KX-012 — CAD Interac Auto Deposit

Released source basis: backend c5480d8005769412b5af27fe22e9c67f35114c50; web f2d81d2b8181859e050fa70834d6306ac83c28d6; mobile d62d1640c4f05355a7315c163d1c2d8f02309966.

This increment adds persistent direct CAD Interac wallet funding, distinct from existing Interac Money Requests and dedicated bank deposits. Provider activation and controlled live-payment acceptance remain separate operational steps.

## Set up CAD Interac Auto Deposit

Confirm a provider-activated receiving email, choose its CAD wallet and manage availability.

### Prerequisites

- SuperAdmin access; an existing approved Blaaiz connection and verified CAD collection wallet configuration.
- The receiving email must already be registered and ACTIVE for your business with Blaaiz. Activation is completed by the provider.

### Procedure

1. **Prepare the receiving email with the provider.** Register the business receiving email in the Blaaiz CAD wallet / Interac Auto Deposit area. Wait for provider activation. A pending, processing, blocked or deleted registration cannot be used for receiving instructions.
2. **Find the registered email.** Open Treasury > Interac receiving. Load/search provider registrations and select the intended email. The registration must belong to the configured provider business. Provider access errors must be resolved on the provider account; do not rotate working credentials as a troubleshooting shortcut.
3. **Save a receiving route.** Choose the verified CAD provider-wallet configuration, registered receiving email, display name and permitted Individual/Business audiences. Enter an audit reason and save the draft. A draft never exposes usable instructions or activates the provider registration.
4. **Verify provider readiness.** Use Verify for the route. KorridorX reads the provider registration and CAD wallet and checks the current provider connection, receiving email and status. An Active checkbox alone cannot establish readiness. Read any failure and correct the provider setup before retrying.
5. **Enable and check the customer experience.** Enable only a successfully verified route in the current connection. Open the intended Business CAD wallet or the current mobile build and view the persistent Interac instructions. Check audience, country/asset deposit permissions and wallet eligibility. Sending a real payment is a separately approved acceptance activity.
6. **Maintain the route.** Pause disables new automatic funding instructions. Reverify after provider changes or a blocked notification. Receiving identity and historical sender/deposit ownership are retained. Create a separate route for a different receiving identity; never repoint historical deposits.

**Expected result:** Eligible CAD wallets show the confirmed active receiving email and their own sender-email registrations. No transfer is required to fund a wallet.

### Operational constraints

- The same sender email cannot identify multiple personal/business wallets sharing a receiving route. A person's verified registration email can reserve the personal CAD wallet even before first opening funding instructions.
- Provider registration lookup requires the granted interac-auto-deposit-email:read scope. KorridorX requests a separate read token and leaves configured payment scopes and credentials unchanged.
- A code proves control of the sending email; it does not prove that a bank uses that email. Advise customers to check their actual bank Interac sender address.
- No migration or release script activates a route, initiates a payment, replays a deposit or changes a balance. Existing Interac Money Requests and dedicated bank deposits retain their existing entry points.

### Troubleshooting

- 401/403 or missing scope: check API access, IP restrictions and the provider's permission grant for registration reads. The application cannot grant itself provider permissions.
- 404 or absent registration: the identifier may be wrong, deleted or from another provider business. Search the configured business's registrations.
- Wallet mismatch or connection changed: choose/verify the correct CAD collection wallet and reverify the route. Credentials/environment changes must not attach old receipts to a new connection.
- Unavailable to a customer: check route status/audience, current connection verification, country/asset deposit eligibility, CAD wallet status and the customer's business context.
- A provider blocked/deleted email must remain unavailable. Refresh/reverify; never use a previously copied email as proof of current readiness.

## Fund a Business CAD wallet with Interac

Use reusable receiving instructions and up to five verified bank sending emails without creating a transfer.

### Prerequisites

- An eligible Business CAD wallet and permission to view it; sender-email changes additionally require the business funding-management permission.
- An active receiving route and access to every email address you register.

### Procedure

1. **Open the wallet's instructions.** Open Business wallets, select the CAD wallet and choose Deposit / Add money. Open Interac Auto Deposit and refresh its receiving status. These instructions stay available independently of any transfer or Money Request.
2. **Check the bank's actual sending email.** Use the email your bank attaches to outgoing Interac transfers. A login, employee, invoice or business contact email may differ. Some fintech apps use a shared intermediary address and cannot be matched automatically; send directly from the bank.
3. **Register and verify an email.** Add a sending email and request its code. Enter the six-digit code sent to that email within 10 minutes. Each code works once. Resend is available after 60 seconds and invalidates the previous code; five incorrect attempts require a fresh code. Pending and verified registrations count toward the five-email limit.
4. **Send CAD from the verified address.** Check that the sender is Verified and the receiving route is Active. Copy the displayed receiving email into your bank's Interac transfer. Use CAD. No KorridorX transfer, quote or payment request is needed for this direct wallet deposit.
5. **Check the confirmed wallet credit.** After bank/provider confirmation, refresh the wallet balance and history. KorridorX verifies the incoming transaction, receiving route, sender and amount. The confirmed net deposit is credited once. Code requests, copied instructions and refreshes do not credit funds.
6. **Maintain sending emails.** Remove emails that should stop matching new deposits. Their original wallet assignment and history remain for audit and are not transferred to another personal/business wallet. Use a different verified sender address or contact support when ownership conflicts.

**Expected result:** Completed, provider-verified CAD receipts from an active verified sender credit only the correct Business wallet once. Unknown or conflicting receipts remain uncredited for audited review.

### Operational constraints

- Up to five pending/verified sending emails per business wallet scope; only Verified active registrations qualify for automatic matching.
- An email owned by a personal CAD wallet cannot simultaneously route deposits to a Business wallet on the same receiving route, even when the same person manages both.
- A deposit sent before the sender registration became effective is retained for review. Adding an email later does not silently claim old deposits.
- Existing Interac Money Requests and dedicated bank-account deposit instructions remain available through their established flows. Check a pending request before making another payment.

### Troubleshooting

- Missing code: check the nominated email and spam folder, then use Resend after the cooldown. Use the newest code. Do not share codes with support.
- Code expired, already used or too many attempts: request a fresh code. Never reuse a code from another email, wallet or business.
- Email already assigned: use another bank sending email or contact support. Changing case, spaces, display name or the amount does not change wallet ownership.
- No receiving email: ask an administrator to check provider activation, current connection, eligible audience and wallet readiness. Do not send to remembered details.
- Confirmed bank deposit missing: give support the bank/provider reference, date/time, CAD amount, sending/receiving emails and intended wallet. Keep the original reference; do not resend money solely to test matching.

## Review an unmatched Interac deposit

Inspect retained deposit evidence and append audit notes without manually crediting or reassigning funds.

### Prerequisites

- SuperAdmin access and the customer's original bank/provider reference.
- A retained incoming deposit in the review list; distinguish a registration-status callback from a payment.

### Procedure

1. **Locate the retained receipt.** Open Interac receiving > Deposit review. Select the review status and use the paginated list. Open the deposit record by its reference/ID. Unknown senders, missing information and provider failures are retained rather than guessed.
2. **Compare independent evidence.** Check the provider transaction ID, reference, status, CAD gross/net/fee amounts, bank sending email, receiving email, provider wallet/connection, intended customer wallet and timestamps. Sender names, notes and amounts alone do not establish ownership.
3. **Read the reason and history.** Inspect verification outcome, matching/duplicate status and existing audit notes. The provider transaction ID identifies a deposit; different webhook event IDs do not make it a new payment. Old unmatched deposits cannot be claimed by registering the email later.
4. **Document the investigation.** Append a clear audit note describing evidence reviewed and the reason for escalation. Notes are retained with the actor and time. The review screen does not contain a balance credit, replay or reassignment action.
5. **Escalate financial resolution separately.** Use the established approved financial-operations process for any refund or exceptional correction. Keep original receipt and ledger references linked in the investigation. Never edit balances, provider credentials, event IDs or ownership tables to force a match.

**Expected result:** Operations can trace why a receipt did not credit and record an audited investigation while the existing deposit and ledger ownership remain protected.

### Operational constraints

- Registration activation/block callbacks have no payment amount or transaction ID and must never credit a wallet.
- A retry with a different event ID must converge on the same provider transaction claim across existing-payment, polling and unsolicited-deposit handling.
- Requests for codes, provider credentials, account passwords, transaction PINs or bank logins are unnecessary and must not be included in support notes.

### Troubleshooting

- A sender address belongs to a fintech rather than the customer: retain the receipt for investigation; ask the customer to use a direct bank transfer with their own verified sender for future deposits.
- A webhook is valid but the provider lookup fails or contradicts its fields: retain the receipt uncredited and investigate provider access/data. Do not infer confirmation from the webhook body alone.
- Already handled by an existing payment: inspect that collection and its ledger reference. Do not create an unsolicited second credit.
- An older route or sender has been removed: historical ownership remains authoritative. Do not attach the receipt to a newly added email/route.

## Personal mobile wallets

Wallets > CAD > Add money > Interac Auto Deposit uses the verified registration email by default when ownership is unambiguous. The registration email must be the actual bank sender. Optional extra sender emails require their own code. A conflict is shown explicitly; the application does not choose between personal and business wallets. The in-app How Interac Auto Deposit works guide explains setup, expiry, removal, missing receipts and support evidence.

## Provider contract and amount policy

The provider's receiving-email registration lookup proves that the registration belongs to the current API business and reports its activation status; that response does not include a wallet ID. Select the correct configured CAD collection wallet when creating the local route. The application verifies that wallet separately and checks both its wallet ID and business ID on each incoming deposit. An incorrect association remains uncredited for audited review. Controlled live-payment acceptance must confirm the actual route after deployment.

The authenticated transaction lookup establishes immutable provider transaction ID, receiving email, sender email, CAD wallet, status and gross/net/fee amounts. New unsolicited wallet receipts credit the verified net amount. Existing Money Requests retain their requested-principal and platform-fee accounting policy. Neither the customer UI nor a registration-status event establishes successful receipt.

## Release and isolation

The package contains an additive schema migration. The existing remote backend CI validates PostgreSQL and the normal staging workflow applies the migration. The PowerShell runner never installs or starts local Docker, applies a local database migration, resets a database, changes configured provider credentials or payment scopes, creates a provider registration, initiates a payment, runs a webhook replay, or writes a wallet balance. It preserves the released KX-011 source, applies only reviewed KX-012 files, and publishes backend then web then mobile after exact-commit workflow gates.

Local automated checks use fake provider handlers and unavailable test-database settings. Database-dependent tests remain a separate remote CI gate unless specifically reported as executed against an isolated disposable test database. A passing build is not evidence of a live payment. New receiving routes start unavailable until an administrator deliberately verifies/enables a provider-activated registration.

## Official provider references

- [Receiving-email registration lookup](https://docs.blaaiz.com/docs/payments/interac-auto-deposit-email/get-an-interac-auto-deposit-email)
- [Registration list](https://docs.blaaiz.com/docs/payments/interac-auto-deposit-email/list-interac-auto-deposit-emails)
- [Transaction confirmation](https://docs.business.blaaiz.com/api-reference/transaction/get-transaction)
- [CAD collection webhook fields](https://docs.business.blaaiz.com/guides/webhooks/collection/cad)
- [Registration status callbacks](https://docs.blaaiz.com/docs/guides/webhooks/interac-auto-deposit-email)
- [Interac sending-email guidance](https://docs.blaaiz.com/docs/guides/use-cases/interac-collections)
- [Interac Money Request fee policy](https://docs.business.blaaiz.com/guides/interac-money-requests)
