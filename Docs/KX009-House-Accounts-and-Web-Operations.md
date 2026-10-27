# KorridorX — House accounts and web operations
Reviewed: 17 October 2026. KX-001–KX-009.

This manual is also published as permission-scoped in-app Help & Guides and is indexed by the existing universal search. It documents operations; reading a guide never performs an action.

## Page length, filters and later history pages (admin)
Page: `/admin/settings`
Guide: `/admin/help/kx009-web-list-pagination`

1. **Set page length** — Open Display settings and select 10, 20, 50 or 100 rows. The preference is saved for your account on this browser, not across devices.
2. **Navigate records** — Return to the list and use Previous and Next. The displayed range and total apply to its current filters. Some short complete lists page in the browser; growing histories request pages from the server.
3. **Keep scope clear** — Search or change filters before browsing pages. Changing the selected business or parent record resets the relevant history and clears data from the old scope. Nested lists keep separate page positions.
4. **Handle refresh failures** — Wait for loading to finish. Use the available Refresh or Retry control after an error. Do not treat stale rows or an empty error state as proof that no records exist.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Reopen the list after changing page length. The server enforces its own maximum. If an expected record is absent, remove filters, check the selected workspace/business and then page further. Report the module, filter, page and record reference.

## Visible validation and immediate save feedback (admin)
Page: `/admin/settings`
Guide: `/admin/help/kx009-web-form-feedback`

1. **Find the invalid field** — Complete required fields. Leave an invalid field or submit the form to see a red border and explanatory message; feedback is not based on colour alone.
2. **Correct and submit once** — Correct email formats, selected master-data assets and other errors. Check the summary and first invalid field. While a save is in progress, do not repeatedly submit.
3. **Check confirmed results** — Successful operations refresh their affected lists or status. Failed operations must retain meaningful feedback rather than pretending the save succeeded.
4. **Report a stuck save** — After a refresh, check whether the record already exists before retrying creation. Report the screen, action, redacted error and correlation ID through Support.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Confirm your role, selected business and current filters. Record the reference and any correlation ID, but never send passwords, bearer tokens or API secrets to support.

## Page length, filters and later history pages (business)
Page: `/business/settings`
Guide: `/business/help/kx009-web-list-pagination`

1. **Set page length** — Open Display settings and select 10, 20, 50 or 100 rows. The preference is saved for your account on this browser, not across devices.
2. **Navigate records** — Return to the list and use Previous and Next. The displayed range and total apply to its current filters. Some short complete lists page in the browser; growing histories request pages from the server.
3. **Keep scope clear** — Search or change filters before browsing pages. Changing the selected business or parent record resets the relevant history and clears data from the old scope. Nested lists keep separate page positions.
4. **Handle refresh failures** — Wait for loading to finish. Use the available Refresh or Retry control after an error. Do not treat stale rows or an empty error state as proof that no records exist.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Reopen the list after changing page length. The server enforces its own maximum. If an expected record is absent, remove filters, check the selected workspace/business and then page further. Report the module, filter, page and record reference.

## Visible validation and immediate save feedback (business)
Page: `/business/settings`
Guide: `/business/help/kx009-web-form-feedback`

1. **Find the invalid field** — Complete required fields. Leave an invalid field or submit the form to see a red border and explanatory message; feedback is not based on colour alone.
2. **Correct and submit once** — Correct email formats, selected master-data assets and other errors. Check the summary and first invalid field. While a save is in progress, do not repeatedly submit.
3. **Check confirmed results** — Successful operations refresh their affected lists or status. Failed operations must retain meaningful feedback rather than pretending the save succeeded.
4. **Report a stuck save** — After a refresh, check whether the record already exists before retrying creation. Report the screen, action, redacted error and correlation ID through Support.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Confirm your role, selected business and current filters. Record the reference and any correlation ID, but never send passwords, bearer tokens or API secrets to support.

## Create Treasury house accounts in bulk (admin)
Page: `/admin/treasury/house-accounts`
Guide: `/admin/help/kx009-provision-house-accounts`

1. **Open provisioning** — Open Treasury → House accounts. Alternatively, open Trading & Business Wallets → Instant and choose Create missing house accounts; the instant pair form is preserved.
2. **Choose assets** — Select 1 to 50 supported assets from configured master data, such as CAD, NGN and USD. You cannot enter arbitrary codes, a personal owner or an opening balance.
3. **Create safely** — Choose Create house accounts once. New accounts are Treasury-owned, House type, Active and have zero settled, available and held balances. Existing non-deleted Treasury/Platform house accounts are retained without changes.
4. **Review outcome** — Read the created/existing counts and status. Repeating the same selection does not create duplicates. Frozen or closed accounts are not reactivated. A reserved/deleted identity requires Treasury review.
5. **Use the accounts** — On the Instant form, the house-account options refresh after successful creation. Select the matching active account on each side. In Treasury, use the paginated directory and asset filter to review balances.

**Warnings:** Provisioning does not fund an account, create an external bank/provider wallet, enable an asset, configure a rate, or activate a trading pair. If any selected asset or reserved identity fails validation, the provisioning transaction does not create part of that batch. Existing accounts are never silently zeroed or re-owned.
**Troubleshooting:** No assets: ask a master-data administrator to configure supported assets. Operations users cannot bypass master-data permissions. Dropdown still empty: ensure the account is Active, House type, Treasury/Platform-owned and in the selected asset; click Refresh house accounts. No funds in Treasury: stop and ask Finance to arrange approved, recorded funding. Do not insert account balances into the database.

## Fund and reconcile company house liquidity (admin)
Page: `/admin/treasury`
Guide: `/admin/help/kx009-house-account-funding`
Reviewed: 2026-10-27

1. **Refresh the provider balance** — Open Treasury and choose Sync provider wallets. The House wallet funding table uses the existing Blaaiz balance service and shows each supported fiat currency separately.
2. **Read the funding position** — Net company funds equal Blaaiz cash minus customer settled balances, identified uncredited incoming deposits and other positive reserved internal balances. Customer settled balances already include holds. Existing house, treasury and revenue allocations reduce the amount available for new funding.
3. **Choose the right funding route** — For company money already recorded in Treasury, use Internal transfers in the same currency. For reconciled company cash held at Blaaiz but not yet recorded internally, choose Fund house and enter the house account, amount, company deposit or opening-balance approval reference, and reason.
4. **Preview and confirm once** — Preview refreshes Blaaiz again and checks the available company amount. Review incoming receipts, fees and other obligations, confirm company ownership, then choose Confirm house funding. The server refreshes balances, rejects a changed or expired preview and records a balanced ledger transaction with an audit trail.
5. **Check the posted result** — Retain the ledger reference and check the house balance. Reusing the same funding reference and details returns the recorded operation rather than crediting it twice. An existing reference with different details requires reconciliation.

**Expected result:** The approved company-owned amount becomes available in the selected house account, with an equal external debit and available credit and a retained ledger/audit reference. No provider transfer is initiated.

**Warnings:** A provider balance or calculated surplus alone does not prove company ownership. Customer funds, held balances, unmatched incoming deposits and other obligations must remain protected. Stale or missing provider balances, invalid ledger balances and incoming receipts without a reliable amount or currency block funding until reconciled. Use Internal transfers for already recorded company funds. Provisioning starts at zero and does not fund an account. Never edit database balances.

**Troubleshooting:** Sync first if the balance is missing or stale. A zero funding limit can mean liabilities or existing company allocations consume the provider balance; a negative net position requires Finance reconciliation. For unquantified incoming receipts, review Provider operations and recover or reconcile the original deposit. Do not classify it as company cash to bypass the block. If balances change or the preview expires after ten minutes, preview again. For an uncertain result, use the original funding reference and details to check whether it already posted.

## Configure an instant conversion from house account to quote (admin)
Page: `/admin/trading-wallets`
Guide: `/admin/help/kx009-instant-pair-end-to-end`

1. **Create the required accounts** — Open the Instant workspace and Create missing house accounts. Provision the source and destination assets or use existing eligible accounts. New accounts start at zero.
2. **Assign the correct sides** — For CAD → NGN, choose CAD as source and the CAD house account to receive CAD. Choose NGN as destination and the NGN house account to supply NGN.
3. **Configure limits** — Set minimum and maximum source amounts and the source increment. Limits must align with the increment. Quote validity is 5–300 seconds. Limits are denominated in the source asset.
4. **Configure pricing and funding** — In FX rates & pricing, configure an active, effective, fresh rate for that direction. Confirm the destination house account has sufficient settled and available funds. Creation of a pair does neither of these.
5. **Activate and verify** — Review asset/channel and country enablement, customer compliance and eligible wallets, then Activate the pair. A status change does not bypass these checks or fund the pair.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** No rate: check the exact direction, effective dates, active flag and freshness policy. Insufficient liquidity: check both settled and available destination balances, not just source liquidity. Ineligible user: check approved KYC/KYB, channel/country permissions and active wallets; do not disable safeguards.

## Bulk pairs, reverse directions and status controls (admin)
Page: `/admin/trading-wallets`
Guide: `/admin/help/kx009-bulk-reverse-pair-controls`

1. **Choose a batch** — Enable Create multiple pairs at once. Choose one source/base asset and several distinct destination/quote assets. Instant destinations each need a matching active house account.
2. **Review reverse pairs** — Also create reverse pairs swaps directions. Reverse pairs are created Paused so direction-specific limits, increments, pricing and liquidity can be reviewed before activation.
3. **Read per-pair outcomes** — Pair creation uses separate requests and may partially succeed. Read created, existing-skipped and failed counts; correct failures and retry without deleting successful pairs. This differs from atomic house-account provisioning.
4. **Use the visible status** — An Active pair offers Pause; a Paused pair offers Activate. Read the current badge instead of inferring status from a button. Changes refresh after success.
5. **Check each direction** — Do not blindly copy source limits or rates to the reverse direction. Its source asset and destination liquidity requirement are different.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Confirm your role, selected business and current filters. Record the reference and any correlation ID, but never send passwords, bearer tokens or API secrets to support.

## Create administrators consecutively and resolve validation errors (admin)
Page: `/admin/users`
Guide: `/admin/help/kx009-admin-user-creation-feedback`

1. **Complete identity** — Open Create administrator. Enter a valid email and the required identity fields, choose the configured country and supply a strong temporary password.
2. **Assign intentional roles** — Select only the roles the colleague needs. Do not give every role simply to remove a validation or access error. Check displayed requirements before submission.
3. **Submit and review** — Choose Create internal user once. Correct field-level and server-side errors, including an existing email, instead of repeatedly resubmitting.
4. **Create another colleague** — After success the dialog closes and the user list refreshes. Reopen the dialog and use a different email. Verify the fields and loading state have reset.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Confirm your role, selected business and current filters. Record the reference and any correlation ID, but never send passwords, bearer tokens or API secrets to support.

## Investigate paginated histories, traces and exception queues (admin)
Page: `/admin/embedded-finance`
Guide: `/admin/help/kx009-history-and-trace-pages`

1. **Set the investigation scope** — Choose the correct business, activity, exception source and search filters. Totals refer to the matching scope rather than a limited recent-record scan.
2. **Browse full histories** — Use each section’s own controls for financial activity, webhook exceptions and provisioning exceptions. Trading settlement exceptions and digital-asset histories also retrieve server-side pages.
3. **Inspect trace sections independently** — Open an activity and page provider transactions/requests, reservations, ledger transactions, journal entries and the audit timeline independently. Opening another activity resets those pages.
4. **Read nested detail safely** — Compliance documents, owners, notes, evidence and matches and other complete detail lists use their own visible pagination. Expanded ledger postings, settlement details, credentials and support evidence retain separate page state.
5. **Preserve evidence** — After a request failure, retry the relevant section. Record the activity ID, scope, section and page; pagination is not proof of settlement or reconciliation.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Confirm your role, selected business and current filters. Record the reference and any correlation ID, but never send passwords, bearer tokens or API secrets to support.

## Support checklist for web updates and house-account setup (admin)
Page: `/admin/support`
Guide: `/admin/help/kx009-web-operations-support-triage`

1. **Identify the symptom** — Record workspace, role, selected business, page, exact action, expected result and time. Ask for a redacted screenshot or correlation ID, not credentials or raw authenticated requests.
2. **Check rendering and forms** — For stale rows use the page refresh after confirmed success. For a stuck administrator form, check field errors, duplicate emails and whether the record already exists. Escalate reproducible errors rather than asking users to keep clicking.
3. **Check pagination and pair state** — Verify saved browser page length, active filters, later pages and current pair status. Bulk pair operations may partially succeed; house provisioning is atomic and retry-safe.
4. **Route house issues correctly** — An empty house-account dropdown needs an eligible same-asset Active House account. Treasury/Operations/Admin/SuperAdmin can provision through House accounts; support roles cannot provision through this guide.
5. **Separate funding and activation** — Zero balance after creation is expected. Insufficient settled destination funds go to Treasury/Finance; missing/stale rates go to FX operations; eligibility failures go to the responsible compliance team.
6. **Close with evidence** — Confirm the relevant screen works after the approved change. Never prescribe database balance edits, broad role grants or bypassing funding/compliance checks.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Confirm your role, selected business and current filters. Record the reference and any correlation ID, but never send passwords, bearer tokens or API secrets to support.

## Use instant conversion, marketplace orders and paginated order books (business)
Page: `/business/trading`
Guide: `/business/help/kx009-business-trading-pages`

1. **Choose the trading mode** — Instant conversion accepts an eligible current quote against platform liquidity. Marketplace trading uses compatible orders; availability and permissions still apply.
2. **Review the pair and wallet** — Choose an available configured pair and eligible wallets. Paused pairs are unavailable for new trading. Insufficient wallet funds or expired quotes must be resolved before another attempt.
3. **Inspect order-book depth** — Bid and ask sides have separate server-side pages. Loading and retry states apply per side; switch pairs before comparing prices and wait for current data.
4. **Review history and RFQ quotes** — Use the history and per-RFQ quote pagination to reach later rows. A visible historical quote or match is not a new executable quote or proof of external payout.

**Warnings:** Use staging test records for acceptance. Do not perform a financial or compliance action merely to test navigation.
**Troubleshooting:** Confirm your role, selected business and current filters. Record the reference and any correlation ID, but never send passwords, bearer tokens or API secrets to support.

## Engineering and support boundary
Provisioning uses the existing FinancialAccounts model, its existing unique indexes, a stable system treasury owner ID, a transaction-scoped PostgreSQL advisory lock, and staged audit records committed with account creation. There is no schema migration, balance input, status-change action, provider wallet creation or automatic liquidity posting.
Existing non-deleted house accounts owned by Treasury or Platform are reused. Frozen/closed accounts keep their status and balances. Deleted canonical identities require explicit review instead of implicit resurrection.
Do not equate pair-bulk creation (per-pair partial success is possible) with account-bulk provisioning (single transaction).
