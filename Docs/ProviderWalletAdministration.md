# Provider wallet administration

SuperAdmin manages Blaaiz wallet registrations at `/admin/provider-wallets`.
The API is `/api/admin/provider-wallet-configurations`; all its actions require
the SuperAdmin role. Customer financial accounts and their ledger remain separate.

## Setup

1. Apply migration `20260921141401_ProviderWalletAdministration` through the normal
   deployment migration process before starting the updated API.
2. Sign in as SuperAdmin and open **Provider wallets**.
3. Enter a reason, then discover provider wallets or register a known provider
   wallet ID. Select its asset and, for crypto, its configured network. This links
   an existing provider wallet; it does not create a wallet at Blaaiz.
4. Verify the registration. The provider must return an active wallet with the
   same ID and asset under the connected credentials. Verification also refreshes
   the displayed provider balance. Provider discovery/verification require Blaaiz
   to be enabled; inactive drafts can be registered while it is disabled.
5. Configure collection/payout permissions, activate, and select the default use.
   One active default per provider, environment, asset, network and purpose is
   enforced by database indexes. Changing a default replaces that default in one
   transaction and is audited. It does not suspend the previous wallet.
6. Test the intended sandbox route. Keep automatic payout dispatch disabled until
   the sandbox acceptance checks have passed.

The UI uses configured assets/networks. This change does not enable new provider
currencies, networks, destinations or gateways. The existing Blaaiz adapter still
defines its supported capabilities. Credentials remain in protected deployment
configuration; wallet IDs are not private keys or credentials.

## Existing environment mappings

`CollectionWalletIds`, `PayoutWalletIds` and `CryptoWalletIds` are retained only
for the explicit **Import existing environment mappings** action. Import is
idempotent and creates inactive, unverified drafts. It does not choose defaults.
Review, verify and activate each required route after importing. Runtime payment
selection never falls back to those environment values or a provider's first
wallet. Blank wallet environment variables can remain blank.

For cross-currency remittance payouts, the selected wallet asset remains the
transfer's **source currency**, matching the existing provider request contract.
Embedded payouts use the payout currency. Crypto uses the asset plus network.
CAD virtual-account provisioning uses the default CAD collection wallet.

## Payment selection and operational changes

Before a provider submission, a separate durable database record binds the
operation to a wallet. A unique operation constraint handles concurrent requests.
The binding is committed independently of the surrounding business transaction,
so its rollback cannot lose the wallet choice after an external timeout.

Retries use their original selection, including when a default has changed.
Suspending the original wallet blocks subsequent submissions/retries rather than
redirecting them. Already dispatched requests may still complete at the provider;
suspension does not cancel an in-flight payment. Configuration does not change
customer balances, reverse transactions or move provider funds.

Old attempted payments without a durable selection stop for reconciliation;
the application must not guess which wallet funded them. Review their original
provider request before deciding how to recover them. There is deliberately no
generic override that silently reassigns an old payment.

Registrations are environment-scoped. Verification is tied to the Blaaiz endpoint
and client ID. Changing either requires verification again; rotating the secret
for the same client does not invalidate the wallet identity. Wallet ID, asset
and network cannot be edited after registration. Register another wallet to
change that identity. Every mutation requires a reason and records an audit event.
Revision checks reject stale edits.

## Validation and rollout

The package includes backend database integration tests for default switching,
suspension, concurrent selection, rollback survival, legacy attempts, verification,
environment/account changes, import idempotency and unique defaults. It also updates
the existing crypto workflow fixtures to use verified database registrations.

Run the complete backend suite against the disposable localhost `korridorx_rc`
database. The existing test fixture resets its public schema; never use a staging
or production connection. Check migration drift, Angular staging build and the
full Angular suite before committing. Provider calls in these tests are mocked.

Staging acceptance: SuperAdmin access; non-SuperAdmin rejection; register/import;
verify; select a default; change a default; suspend a wallet; inspect audit history;
then exercise sandbox collection/payout webhooks and reconciliation. Real provider
acceptance remains a separate step after code validation.

This migration creates two new tables and does not rewrite ledger data. Keep the
selection records once provider activity starts. Dropping the new tables during
a rollback would discard payment routing history.
