# Provider-managed FX rates and support guide

Reviewed: 2026-10-20.

KorridorX obtains provider-priced FX rates through a live server request for each new quote or preview. An administrator selects an existing trading pair and provider, configures customer markup and optional bounds, then activates that configuration after a successful provider check. Stored rate history and scheduled snapshots do not substitute for a failed live request.

This guide describes the implemented workflow. It does not certify any environment's current provider grant, connection or active configuration.

## Pair selection and activation

Open **Admin → FX rates and pricing** at `/admin/fx`. Operations, Admin and SuperAdmin can use this page. **Existing pair** reuses the Instant and Marketplace catalogs from **Trading & business wallets**; FX configuration does not create a second pair catalog.

The source/destination direction is fixed by the selected pair. The rate means destination units per one source unit. A reverse direction requires its own existing pair and provider configuration; the service does not invert or triangulate rates.

Only one configuration can be active for a direction, even if the same direction appears in both catalogs. A draft may be prepared while its pair is inactive, but activation requires an active pair with supported assets. A saved configuration's pair cannot be changed.

1. Select **Existing pair** and **Provider**.
2. Enter **Markup %** and optional customer-rate bounds. Leave **Provider rate ID** empty for normal use. If multiple usable provider rows match the exact direction, select the intended provider-approved ID and fetch again.
3. Choose the refresh schedule.
4. Select **Fetch live preview** and inspect pricing, capabilities and timestamps. Editing a form setting clears that preview.
5. Select **Save draft**. The saved configuration is inactive.
6. Select **Activate** on the saved configuration. Activation performs another live check before replacing an existing active configuration. Replacement is atomic; a failed replacement check leaves the previous active configuration in place.

An active configuration cannot be edited in place. Create and activate a replacement draft, or deactivate the existing configuration before editing it. Deactivating the only active configuration stops new quotes in that direction. Changes use revision checks; reload the page when another operator has changed the configuration.

Sources: [FX configuration service](../Services/Fx/FxProviderRateService.cs), [configuration mapping and uniqueness constraint](../Data/Configurations/FxProviderRateConfigurations.cs), [admin API](../Controllers/AdminFxOperationsController.cs).

## Customer pricing and fees

The server applies the provider configuration markup once:

```text
unbounded customer rate = provider rate × (1 − markup percentage / 100)
customer rate = optional minimum/maximum bounds applied to that result
saved customer rate = round to 8 decimal places, midpoint away from zero
```

For example, a provider rate of `1.25` with `2%` markup produces `1.225` before optional bounds. This example is arithmetic, not a current provider quote.

Markup must be at least zero and below 100%, with at most six decimal places. Bounds must be positive, with minimum no greater than maximum. Provider rates and bounds must fit ten whole digits and eight decimal places. A result that rounds to zero is rejected.

The provider rate is read-only in the client. Web and mobile display the authoritative rate and amounts returned by the backend; they do not apply another copy of the provider markup. Existing business Instant pricing adjustments still apply after this base customer rate under the existing business policy, including its spread/revenue accounting.

FX-provider identity stays on the rate and observation; a transfer quote separately records the existing remittance execution provider (currently Blaaiz). Adding an FX rate adapter therefore does not change payout routing, recipient verification or the provider that executes a transfer.

Transfer fee selection and the existing rounding rules of each workflow are unchanged. Fees remain separate from the FX markup. Marketplace limit orders and counterparty RFQs retain their existing pricing workflows; choosing a Marketplace catalog pair for provider configuration does not replace their order prices with a provider rate.

Sources: [FX pricing calculation](../Services/Fx/FxPricing.cs), [consumer transfer quotes](../Services/Fx/TransferQuoteService.cs), [business transfer quotes](../Services/BusinessTransfers/BusinessTransferService.cs), [business pricing](../Services/EmbeddedFinance/BusinessPricingService.cs), [Instant quoting and execution](../Services/Instant/InstantTradingService.cs).

## Scheduled refresh and on-demand freshness

| Setting | Implemented behavior |
|---|---|
| Default | `Interval`, every **180 minutes**, timezone `UTC`. |
| Interval | A whole number from **1 to 10,080 minutes**. |
| Daily | **1 to 24 distinct** `HH:mm` times in `UTC` or a supported IANA timezone such as `America/Toronto`. |
| Off / `Disabled` | No background refresh; the configuration can remain active for live quoting. |
| Inactive draft | No scheduled publication. A manual check can inspect the draft without activating it. |

Daily schedules follow the selected timezone. A nonexistent local time during a spring-forward gap is skipped; an ambiguous fall-back time runs at its first occurrence. The worker checks for due work every 30 seconds, so scheduled times are due times rather than a promise of execution at the exact second. Each due configuration has a lease to avoid duplicate concurrent refreshes. A failed scheduled refresh records an error and is scheduled to retry after five minutes.

Schedules maintain operational snapshots. **Every new provider-priced consumer transfer, business transfer, business batch quote or replacement, embedded transfer, personal/business Instant quote, and public indicative transfer preview still performs a fresh provider read, regardless of the last scheduled success.** Admin previews, manual checks and activation also fetch the provider.

Consumer wallet valuation fetches each distinct direction required for a **nonzero foreign-currency available balance** when displaying the combined estimate in a chosen supported fiat wallet currency. Native-currency and zero-balance components need no FX request. If any required component cannot be freshly priced, its estimate is unavailable and the combined total is withheld. Original wallet balances remain visible and unchanged; a valuation does not convert or move money.

A timeout, denied grant, missing active configuration, invalid/ambiguous rate or incompatible capability blocks a new quote. There is no fallback to an older `ExchangeRate` row or a previous successful client preview. A previous success does not prove the next check will succeed. The service may reuse a still-valid OAuth token, but it does not reuse an earlier FX result to satisfy a new rate request.

Sources: [schedule validation and timezone rules](../Services/Fx/FxRateSchedule.cs), [refresh worker](../BackgroundJobs/FxRateRefreshWorker.cs), [live service](../Services/Fx/FxProviderRateService.cs), [wallet valuation](../Services/Wallets/ConsumerWalletValuationService.cs), [valuation calculator](../Services/Wallets/ConsumerWalletValuationCalculator.cs).

## Provider time, fetch time and quote snapshots

The live preview shows separate evidence:

| Value | Meaning |
|---|---|
| Requested / `RequestedAtUtc` | When KorridorX started the rate HTTP request. |
| Received / `ReceivedAtUtc` | When KorridorX received the rate response. |
| Provider updated / `ProviderTimestampUtc` | The provider's `updated_at`, converted to UTC when supplied. It can be absent. |
| Last attempt / Last success | Operational metadata for the configuration's checks. |

A fresh HTTP check establishes retrieval time. It does not prove that the provider changed its price at that moment. Provider time is preserved separately and is never replaced with server time to make the rate appear newly updated. An invalid or timezone-unqualified supplied provider timestamp is rejected.

A successful saved quote records its rate, amounts, fees and expiry plus the immutable observation ID. A later scheduled refresh, provider change or configuration activation does not rewrite those economics. Transfer quotes keep their existing validity durations; Instant validity starts after the live fetch and current-state checks. Execution still applies existing expiry, ownership, approval, account, balance and liquidity controls. A valid quote is not a promise that those other execution conditions will remain satisfied.

The provider read occurs before the short pair-lock database transaction. Configuration revision/connection/pair checks and observation, rate and quote persistence are coordinated afterward. Instant quoting also rechecks the requested Instant pair, current asset/account eligibility and liquidity after the provider fetch. Batch quote persistence uses a separate database scope so it cannot flush pending approval, transfer or reservation changes in the batch context.

Sources: [provider parser](../Providers/Fx/BlaaizFxRateParser.cs), [provider request](../Providers/Fx/BlaaizFxRateProvider.cs), [observation model](../Models/Fx/FxRateObservation.cs), [live transaction](../Services/Fx/FxProviderRateService.cs), [Instant service](../Services/Instant/InstantTradingService.cs), [batch service](../Services/BusinessTransfers/BusinessPaymentBatchService.cs).

## Blaaiz access and connection isolation

FX reads use a separate OAuth client-credentials token requesting **`rates:read`** at the configured token endpoint. The rate request is an authenticated `GET` to `/api/external/rate?search_term=SOURCE/DESTINATION`. Existing collection, payout, Interac and wallet token scopes are not changed by this integration.

The credentials must actually have the `rates:read` grant at Blaaiz and satisfy that account's API/IP access rules. Requesting the scope locally cannot grant it. Existing `wallet:read`, collection or payment access does not demonstrate rate-read permission. If the OAuth response supplies a scope, the implementation requires the exact requested `rates:read` scope.

The effective API and token endpoints must use HTTPS on the same provider authority. Credentials and endpoints are read from the existing provider configuration; this workflow does not replace them or accept credentials in its FX form. The authentication cache and activated configuration are isolated by a hash of the effective environment, API endpoint, token endpoint, client identity, secret fingerprint and scope. Raw secrets do not appear in that key or API DTOs.

Changing the effective environment, host, token endpoint, client ID or secret creates a different connection identity. Live use of an old activation is rejected until an administrator activates the configuration again against the new connection. Preserve the established staging production-provider connection and its isolation controls; do not point it at a development provider to work around a rate-read error.

Token responses and bearer tokens must not be included in support evidence. The FX token service does not record OAuth response bodies in provider logs or exceptions. Provider rate requests have their own redacted request audit records.

Sources: [effective connection boundary](../Providers/Fx/BlaaizFxConnection.cs), [separate OAuth token service](../Providers/Fx/BlaaizFxRateTokenService.cs), [rate provider](../Providers/Fx/BlaaizFxRateProvider.cs).

## API and legacy history

The administration base is `/api/admin/fx` and retains the existing Operations/Admin/SuperAdmin authorization.

| Method and path | Purpose |
|---|---|
| `GET /options` | Existing pair catalogs and installed providers. |
| `GET /configurations` | Configuration state, revision, schedule and latest check metadata. |
| `POST /preview` | Fresh pricing preview without activating or creating an executable quote. |
| `POST /configurations` | Save an inactive draft. |
| `PUT /configurations/{id}` | Edit an inactive draft with its revision. |
| `POST /configurations/{id}/activate` | Fresh verification and activation for the direction. |
| `POST /configurations/{id}/deactivate` | Stop live use and scheduled publication for that configuration. |
| `POST /configurations/{id}/refresh` | Fresh check of the saved configuration. |
| `GET /rates`, `GET /markup-rules` | Retained rate and legacy markup history. |
| `GET /dashboard` | Operational rate/configuration summary. |

Legacy manual rate creation (`POST /rates`) and legacy markup writes (`POST /markup-rules`) now reject writes with guidance to use provider configurations. Their historical records remain readable and do not supply new quote pricing. Individual deactivation remains available for legacy rate rows; provider-managed rows must be stopped through their configuration so live pricing and scheduled refresh are disabled together.

Sources: [admin controller](../Controllers/AdminFxOperationsController.cs), [legacy FX operations](../Services/Fx/FxOperationsService.cs).

## Support procedure

For a business transfer, start from `/business/transfers`. For an Instant conversion, use `/business/trading`. Keep the selected business, exact direction and original request amount with the case.

1. **Identify the operation.** Distinguish a new quote, admin preview, wallet estimate and an already-created transfer or trade. Inspect an existing record before retrying an uncertain creation/execution response.
2. **Read the configuration.** Confirm the exact direction, active status, selected pair status, provider/rate ID, last error and revision. The reverse direction does not satisfy this request.
3. **Check access and capability.** Confirm actual `rates:read` grant and API/IP access. Transfers and public transfer previews require the selected provider row's send capability; Instant requires its swap capability. A parser match must be active and usable for the exact direction and selected ID.
4. **Respect connection changes.** If connection rotation is reported, use reactivation to verify the current connection. Do not alter isolation keys or substitute a cached rate.
5. **Retry the read after correction.** Request a new quote or refresh the estimate; do not execute a payment or conversion simply to diagnose pricing.

Useful evidence is the configuration ID, pair/catalog identity, direction, amount, provider rate ID, UTC request time, displayed error, provider request/correlation reference and the distinct provider timestamp when available. Never request client secrets, tokens, passwords or authorization headers.

| Message or symptom | Action |
|---|---|
| `Live FX pricing is not active for this currency pair...` | Activate a reviewed configuration for that exact direction. |
| `The FX provider connection is not configured or enabled.` | Inspect effective configured provider readiness through the authorized operational process. |
| `The FX provider connection changed...` | Reactivate using the current connection; review changed environment/credentials first. |
| `A fresh FX rate is unavailable...` | Inspect the redacted provider request audit and actual scope/access/availability; retry after correction. |
| Multiple usable provider rates | Select the intended explicit provider rate ID and fetch again. |
| Provider not enabled for sending or Instant conversion | Use a provider-approved rate with the required capability. |
| Configuration, revision or pair changed during the request | Reload the configuration/current pair and request again. |
| Newer FX check completed first | Retry; the older response is rejected to preserve newer pricing. |
| Combined wallet estimate unavailable | Inspect the nonzero unpriced component; restore its directional live configuration/access. Do not modify balances to repair a display estimate. |

Configuration edits, activation, deactivation and refreshes are audited. Previewing, saving/activating pricing and reading quotes do not initiate payments or change balances. Deployment does not automatically activate a configuration; any controlled payment acceptance remains a separate operation.

Sources: [live FX checks and safe errors](../Services/Fx/FxProviderRateService.cs), [provider parsing and capability selection](../Providers/Fx/BlaaizFxRateParser.cs), [wallet estimate failure handling](../Services/Wallets/ConsumerWalletValuationService.cs).
