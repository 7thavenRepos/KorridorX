# KX-010 — Add multiple assets to one country

## Administrator setup
Open **Master data → Country & asset mappings** as Admin or SuperAdmin. Select the configured country first. The **Assets not yet mapped to this country** selector contains only configured assets that have no existing country mapping. Mappings with all capabilities disabled still count as existing; table pagination does not affect this filter.

Select up to 100 assets together. On desktop hold Ctrl (Windows) or Command (Mac) while choosing multiple options. All selected assets share the capability choices on this submission. Enter the required reason and choose **Add asset mappings** once. Use separate batches when assets need different capabilities. Selecting a new country clears previous asset selections.

The response displays created and already-mapped counts. Confirmed rows appear immediately, the country stays selected, and mapped assets disappear from the add selector. Refresh the list to pick up other administrators' changes. Existing rows remain editable with **Configure**; country and asset identity remain immutable while editing.

## Existing mappings and defaults
Bulk Add only creates missing mappings. Retrying or simultaneous requests never overwrite existing capabilities, defaults or disabled mappings. Duplicated codes in one request are normalized and deduplicated. New mappings start **non-default**; the country's current default is preserved. Change a default deliberately through **Configure** after creation.

## Validation and support
No available assets: choose a country, wait for successful loading, then check whether all configured assets are already mapped. A failed refresh disables creation until the mapping data loads successfully. Do not duplicate master-data assets to make them reappear.

The backend validates all new assets before writing any new mappings. An invalid country, missing asset, unsupported capability or missing reason prevents creation. No capabilities are silently downgraded. The error identifies an incompatible asset. Existing mappings are skipped without changing their settings.

An unsupported country/asset may only have disabled capabilities. Send/Receive must respect the country settings; Deposit/Withdraw/Trading/Instant must respect the asset settings. This mapping does not override customer compliance, organization permissions, asset channel rules, provider enablement or liquidity checks. It does not create wallets, fund accounts, configure rates or activate trading pairs.

If a save response is lost, refresh and inspect the table before retrying. Report the country, asset codes and redacted error; do not share passwords, tokens or personal customer data. Database uniqueness and a transactional insert-and-audit operation protect against duplicate creation. PostgreSQL integration runs through the existing CI, not a local schema reset.

## Browser acceptance
1. With an existing CA/CAD mapping, select Canada. CAD is absent from Add even if the CAD mapping is disabled or on another table page; assets mapped only to a different country remain selectable.
2. Select two unmapped configured assets and a compatible shared capability set. Save once with a reason. Two rows appear immediately, neither can be selected again, and the country is retained.
3. Switch countries: old selections clear and the option list recomputes for that country. When all assets are mapped, the explicit empty state appears.
4. Use Configure on an existing mapping. Existing edit and default behavior still work. Adding a batch never changes a prior default.
5. Attempt a new batch with one capability-incompatible asset. Read the asset-specific error, confirm neither new row was saved, correct selection and retry.
6. Refresh after another administrator creates a selected mapping; it is removed from selections. A replay/concurrent request reports existing items without overwriting them.
7. Search Help for “multiple assets”, “unmapped assets” or “country asset mapping”; read the updated setup steps. Support triage has the corresponding troubleshooting without extra master-data permissions.
