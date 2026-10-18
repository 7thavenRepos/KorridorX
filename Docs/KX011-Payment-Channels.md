# KX-011 — Blaaiz payment channels and wallet deposits

Status: implementation package; runtime and provider acceptance must be recorded separately.

## Scope and capability boundaries
Blaaiz is the only integrated provider. The data model carries provider identities so a future adapter can be added without hard-coding its name into customer forms. This package does not implement Flutterwave, ACH/wire payout, business raw-card entry, fees, or a standalone fiat cash-out screen. Existing CAD Interac and NGN/CAD transfer payouts remain until the administrator explicitly activates a managed scope.

| Flow | Individuals/mobile | Business web | Important boundary |
|---|---|---|---|
| CAD Interac request | Yes | Yes | Must use the default verified CAD collection wallet; provider endpoint does not accept arbitrary wallet routing. |
| NGN or USD native card | Existing native flow | Not offered | Business needs an approved hosted/tokenized integration first. |
| NGN dedicated bank account | Yes | Yes | Verified KYC/KYB and provider feature/scope are required. |
| USD dedicated bank account | Yes | Not offered | Verified individuals only. USD development deposits do not settle. |
| EUR / GBP dedicated bank account | Yes, production eligible | Not offered | Creation is on hold in api-dev; keep blocked there. |
| NGN bank / CAD Interac transfer payouts | Existing flow | Existing flow | Destination rules plus source-funded provider wallet. |
| Other payout rails | Blocked | Blocked | Listing provider marketing capabilities is not implementation. |

A provider-supported channel is not guaranteed approval for your merchant or customer. SuperAdmin approval confirmation is an attestation. Activation also checks provider connection, verified active wallet, purpose, asset, country mapping and audience. No channels or account balances are seeded by the migration.

## Configure Blaaiz deposit channels and transfer payouts

Choose multiple provider-backed routes, verify readiness, and safely hand an asset from legacy methods to managed channels.

### Prepare prerequisites
Use SuperAdmin. Configure the asset and country/asset mapping, enable the intended audience and deposit/withdrawal operation, and discover/register/verify the provider wallet in the current environment. Provider wallets are not House accounts.

### Create a draft route
Open Payment channels. Select Deposit or Withdrawal / transfer payout, the asset, country (or All eligible mapped countries), Blaaiz, a compatible provider wallet, and an implemented capability. Route identity cannot be edited later; create a new route and pause the old one.

### Set presentation and audiences
Choose the customer-facing name, instructions, display order and eligible audiences. Amount limits apply to fixed-amount collections/payouts. Dedicated bank accounts use no fixed deposit amount and therefore cannot enforce transaction limits in this form. Existing compliance controls still apply.

### Review readiness
Confirm provider feature approval only after checking your provider account. Review blocked reasons and warnings. CAD requests use the verified default CAD collection wallet; the Interac endpoint cannot arbitrarily select another pooled CAD wallet. Business card checkout is not implemented. Business virtual accounts are NGN-only.

### Activate the complete intended set
Prepare the required countries and audiences before activation. The FIRST activation manages the whole asset/direction in this environment, replacing legacy discovery for that scope. Pausing every channel leaves no methods; it deliberately does not restore legacy methods. Drafts alone do not change existing methods.

### Test the correct client
Business: open Business wallets, select a wallet and choose Deposit. Mobile: install the new staging build, open a wallet and Add money. A browser refresh does not update an installed mobile binary. New bank-account deposits do not appear in a transfer-specific fixed-amount checkout; fund the wallet first and then pay from it.

### Separate payout routing
Withdrawal configuration governs the existing remittance transfer payout, not a new cash-out-from-any-wallet screen. Country means recipient destination. The configured funding wallet must match the transfer source asset. Routes are ranked by display order; the saved payout keeps its selected route. Current executable payouts are NGN bank transfer and CAD Interac only.

### Warnings
Blaaiz only. No Flutterwave or other-provider integration is included.

No balances are fabricated, and account creation does not credit funds. Only provider-confirmed, identity/amount/currency-verified receipts may credit the assigned wallet.

Provider api-dev: NGN virtual accounts can receive test deposits; USD creation works but deposits do not settle; GBP/EUR creation is on hold. Do not send real funds to development details.

No new channel fees or currency conversions are introduced. Existing collection fee/ledger policies remain.

Changing credentials does not reassign an old receiving account or payment to another provider connection.

### Troubleshooting
No channels: check first-activation handover, active/ready state, selected asset, profile country, audience, country mapping and wallet status.

Blocked: read the precise reason. A checkbox is not evidence of provider approval. Correct configuration and use Refresh; never bypass it with database edits.

Bank account pending: Check account status. USD/GBP/EUR require verified individuals and complete currency-specific customer fields. NGN also supports verified businesses. A provider rejection needs customer-data review; a timeout requires lookup, not repeated creation.

Interac outcome unknown: retain the existing collection ID and check its status. Do not create a second charge. A durable pending submission may need operations reconciliation.

Pause stops new discovery/initiation but does not discard valid deposits arriving at previously issued bank accounts. Reconciliation and existing payment ownership remain intact.

## Deposit into a business wallet

Use backend-configured Interac or eligible dedicated bank details and recover interrupted funding safely.

### Choose the correct business and wallet
Open Business wallets and select the intended wallet. The selected business and wallet determine access. ViewWallets is required to read; ManageFunding is additionally required to start a deposit or create receiving details.

### Choose an available channel
The Deposit section lists only supported active channels. Select the customer-facing option. Two methods with similar names keep distinct channel IDs. An empty list is not a request to enter your own provider or bank details.

### Use Interac
For CAD, enter a positive amount and valid payer email, then send the Interac request once. The browser retains a reference before initiation. Follow the provider instructions and check payment status; an email or redirect is not confirmation of funds.

### Use NGN bank deposits
Select an NGN dedicated-bank-account channel and Set up bank account. Complete KYB first. Wait until the account is Active and verified bank details are shown. Send only the indicated currency to those details. Creation starts with no wallet credit.

### Recover or refresh
Use Check payment status or Refresh status for an existing attempt/account. If initiation times out, read the saved payment rather than creating another. Refresh the wallet after provider confirmation. Switching business cancels old requests and clears old business details.

### Warnings
Blaaiz business card checkout and USD/GBP/EUR virtual accounts for type=business are not available in this release. NGN dedicated accounts and CAD Interac are the connected business deposit methods.

Use only ready customer-specific bank details. Treasury/provider pooled accounts and House accounts are not a customer deposit address.

Never send real funds to provider-development details. Do not share passwords, card data, access tokens or API keys with support.

### Troubleshooting
No methods: ask an administrator to review the active payment channels, business audience, mapping, provider wallet and approval.

Pending bank setup: refresh; do not repeatedly create accounts. A provider rejection may require additional business information or corrected KYB.

A confirmed deposit missing from balance requires the business/wallet currency, collection or bank-account reference, amount, time, and a redacted error. Do not submit a second deposit merely to test a refresh.

## API and immutable route contract
Admin: `GET/POST /api/admin/payment-channels`, `GET /setup`, `PUT /{id}`, `PUT /{id}/status`. SuperAdmin only. Revision conflicts require refresh. Country/asset/capability/provider-wallet identity cannot be edited. Use an audited reason for creation and status changes.

The legacy collection method DTO retains its existing fields and adds `channelId`, `flow`, description and optional limits. `Collection` flows submit their stable `channelId`; `VirtualAccount` flows create/read receiving details instead of fixed-amount collection. Unknown flows must not render as executable. Native and Business include the same channel identity in initiation. The server controls provider credentials and routing.

Consumer: existing `/api/wallets/{walletId}/funding-methods` and collection endpoints; dedicated account endpoints are `/api/wallet-deposit-accounts/{walletId}`. Business endpoints are `/api/business/payment-channels/wallets/{walletId}/...`, scoped by the established business-context header and permissions. Read/status remains owner-scoped when new deposits are disabled.

A saved channel selection includes provider, environment, wallet, capability, audience/country and amount. Retries cannot silently switch provider wallets. First activation manages the asset AND direction across countries/audiences in that environment, not just the single row. Prepare all intended routes before the first activation. Pausing does not restore legacy discovery. Existing pre-KX-011 operations retain their original selection/reconciliation behaviour.

## Receiving-account and payment safety
Bank provisioning commits its local customer/wallet identity before contacting the provider. One customer/provider-wallet combination is reused, not assigned to another financial wallet. A timeout permits lookup, not blind repeated creation. Pending bank details are not displayed. Ready events are identity checked. Receiving details cannot cross a provider-connection change.

A signed successful deposit event requires exact receiving-account identity and provider transaction verification. Amount, currency, successful state and returned identity fields must match. A database receipt, wallet lock and atomic ledger commit prevent duplicate credit and lost concurrent increments. Unverified or frozen owners/wallets retain the event for reconciliation; no balance is created by a redirect. Paused new channels do not discard verified payments already received into previously issued accounts. Existing gross collection/fee accounting is preserved; the patch does not introduce a new fee policy.

Native card fields keep their existing secure lifecycle and are not persisted in drafts. Business does not collect PAN/CVC. Browser recovery stores only IDs, amount and method in the current business/wallet scope. Status controls do not resubmit a provider payment.

## Database and release
One additive EF migration creates PaymentChannels, PaymentChannelSelections and WalletDepositAccounts and adds channel/reference metadata to Collections and Payouts. It does not modify balances, seed active routes or create provider accounts. Local tests skip the configured RC connection. Existing backend CI supplies PostgreSQL, applies migrations and tests, and gates its established staging deployment. No local Docker or local schema reset is introduced.

Deploy backend before frontend and mobile. The existing mobile workflow produces a staging debug APK, NOT a Play Store or App Store release. Install the new APK from that exact CI run or use your normal staging Flutter run workflow. A web hard-refresh alone cannot update mobile code.

## Acceptance checklist
Record Passed / Failed / Not tested. Use approved staging fixtures and provider-development flows only.

| Check | Expected |
|---|---|
| Create a Draft CAD channel | No existing-method handover and no provider call. |
| Activate CAD for the required countries/audiences | Stable channel shown in Business and updated mobile; correct verified default wallet. |
| Pause all managed CAD deposit channels | New discovery is empty; no legacy fallback. Existing receipt remains readable. |
| Two eligible channels with the same method | Distinct selections; server rejects an ambiguous enum-only submission. |
| Configure NGN bank deposits | Ready bank details only after approved KYC/KYB and provider confirmation. Creation alone leaves balance unchanged. |
| Replay a successful NGN test-deposit webhook | One provider receipt and one credit; wrong amount/currency/account rejected. |
| Switch business or wallet while loading | No old-scope rows or bank details appear. |
| Test a read-only business member | Can read permitted details/status, cannot provision/submit. |
| Interac interruption | Same saved payment recovered; unknown provider submission not charged a second time. |
| Try unsupported Business USD/GBP/EUR VA or web Card | Activation/discovery does not falsely offer unsupported flows. |
| Provider api-dev EUR/GBP | Blocked; no suggestion to bypass by using real funds. |
| Mobile and documentation | New build installed; prior wallet/trading/PIN/biometric flows intact; Help finds payment channels and bank-deposit troubleshooting. |

## Provider references checked for this implementation
Official Blaaiz Virtual Bank Accounts and FAQs, checked 2026-10-18:
https://docs.blaaiz.com/docs/guides/virtual-bank-accounts
https://docs.blaaiz.com/docs/guides/faq/virtual-bank-accounts
https://docs.blaaiz.com/docs/guides/use-cases/virtual-accounts-deposit-collection

Provider rules can change; recheck approval and environment restrictions before enabling production channels. This guide never authorizes a production transfer or test of real funds.
