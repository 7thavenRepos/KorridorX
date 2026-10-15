# Asset Channel Availability V1

- Every globally `IsSupported` asset with `IndividualEnabled` appears in both From and To selectors for every supported country on existing Flutter clients.
- `BusinessEnabled` controls the business channel. `/api/lookups/assets?channel=business`, `/api/lookups/asset-networks?channel=business`, and `/api/lookups/corridors?channel=business` require an authenticated Business, BusinessAdmin, Admin, or SuperAdmin role. Business web callers explicitly request the Business channel. Missing channel always defaults to Individual for backward-compatible Flutter, including dual-role users. Admin clients may request `channel=admin` to list all assets.
- `CanSend` / `CanReceive` from `/api/lookups/corridors` are **selection compatibility flags only**. `ActualCanSend` / `ActualCanReceive` expose country mapping availability. DO NOT use the display flags as authorization to execute transfers.
- Consumer and business quote creation plus transfer creation recheck relevant asset channel flags and existing operational country-asset routes. Current instant trading and new consumer fiat wallet creation enforce channel flags. This is not a provider-enablement change.
- Previously issued wallet history and balances stay visible when an asset is disabled for new operations.
- No crypto network, provider, route, FX rate, wallet, or fee becomes active as a result of an availability toggle. Quote failures must remain explicit for unsupported combinations.
- Defaults: existing rows and new assets have both channel flags true; Admin toggles can turn either off independently.
- Migration `20261015170000_AddAssetChannelAvailability` adds two boolean columns with true defaults. Apply through the existing backend CI staging migration flow after local validation and publication.
- Verify Flutter compatibility against different user/recipient country pairs and the exact seven expected eligible assets, and test supported vs unsupported execution outcomes separately.
- Changing legacy public corridor lookup output may affect other consumers; the staging review MUST include recipient editing and legacy lookup clients.
