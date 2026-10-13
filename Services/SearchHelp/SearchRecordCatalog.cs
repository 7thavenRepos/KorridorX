using KorridorX.Data;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;
namespace KorridorX.Services.SearchHelp;
// Every category has an explicit read permission and ownership predicate BEFORE counts/projections.
// Bind every SearchRecordRow property in each Select. C# property initializers are not
// part of the EF projection and cannot be used by a later Where/OrderBy/record lookup.
// GUID-backed sources use the same Guid.ToString() key that ToDto previously supplied
// after materialization; code-backed master data keeps its existing code key.
public sealed class SearchRecordCatalog(AppDbContext db)
{
    public IReadOnlyList<SearchRecordSource> Sources(SearchScope scope)
    {
        var rows = new List<SearchRecordSource>();
        var businessId = scope.BusinessId;
        var canTransfers = scope.Can(BusinessPermission.ViewTransfers);
        var canWallets = scope.Can(BusinessPermission.ViewWallets);
        if ((scope.IsAdmin && scope.HasRole("Operations", "Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewTransfers))
            rows.Add(new SearchRecordSource("transfers", "Transfers", scope.IsAdmin ? "/admin/providers" : "/business/transfers",
                db.Transfers.AsNoTracking().Where(x => !x.IsDeleted && (scope.IsAdmin || (x.BusinessProfileId == businessId && x.BusinessCustomerId == null)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = (x.BusinessBeneficiary != null ? x.BusinessBeneficiary.Name : (x.Recipient != null ? x.Recipient.FirstName + " " + x.Recipient.LastName : "")) + " " + x.SourceCurrencyCode + " to " + x.DestinationCurrencyCode, Amount = x.SourceAmount, AssetCode = x.SourceCurrencyCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(TransferStatus), scope.IsAdmin ? null : "/business/transfers/"));
        if ((scope.IsAdmin && scope.HasRole("Operations", "Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewTransfers) || scope.Can(BusinessPermission.ViewWallets))
            rows.Add(new SearchRecordSource("collections", "Collections", scope.IsAdmin ? "/admin/providers" : "/business/funding",
                db.Collections.AsNoTracking().Where(x => !x.IsDeleted && (scope.IsAdmin || (canTransfers && x.Transfer != null && !x.Transfer.IsDeleted && x.Transfer.BusinessProfileId == businessId && x.Transfer.BusinessCustomerId == null) || (canWallets && x.FinancialAccount != null && !x.FinancialAccount.IsDeleted && x.FinancialAccount.OwnerType == FinancialAccountOwnerType.Business && x.FinancialAccount.OwnerId == businessId)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.CurrencyCode + " collection", Amount = x.Amount, AssetCode = x.CurrencyCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(CollectionStatus)));
        if (scope.Can(BusinessPermission.ViewBeneficiaries))
            rows.Add(new SearchRecordSource("beneficiaries", "Beneficiaries", "/business/beneficiaries",
                db.BusinessBeneficiaries.AsNoTracking().Where(x => !x.IsDeleted && x.BusinessProfileId == businessId)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Name, Reference = "", Context = (x.Nickname ?? "") + " " + x.CountryCode, Amount = null, AssetCode = "", StateNumber = null, StateText = x.IsActive ? "Active" : "Inactive", Key = x.Id.ToString() }),
                null));
        if (scope.Can(BusinessPermission.ViewBatches))
            rows.Add(new SearchRecordSource("batches", "Payment batches", "/business/batches",
                db.BusinessPaymentBatches.AsNoTracking().Where(x => !x.IsDeleted && x.BusinessProfileId == businessId)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Name, Reference = x.Reference, Context = x.SourceCurrencyCode + " payment batch", Amount = x.TotalPayableAmount, AssetCode = x.SourceCurrencyCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(BusinessPaymentBatchStatus), "/business/batches/"));
        if ((scope.IsAdmin && scope.HasRole("Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewWallets))
            rows.Add(new SearchRecordSource("wallets", "Wallets", scope.IsAdmin ? "/admin/trading-wallets" : "/business/funding",
                db.FinancialAccounts.AsNoTracking().Where(x => !x.IsDeleted && x.OwnerType == FinancialAccountOwnerType.Business && db.BusinessProfiles.Any(b => b.Id == x.OwnerId && !b.IsDeleted) && (scope.IsAdmin || x.OwnerId == businessId))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.AssetCode + " wallet", Reference = x.AccountCode, Context = x.Asset.Name, Amount = x.AvailableBalance, AssetCode = x.AssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(FinancialAccountStatus)));
        if ((scope.IsAdmin && scope.HasRole("Support", "Operations", "Admin", "SuperAdmin")) || !scope.IsAdmin)
            rows.Add(new SearchRecordSource("tickets", "Support tickets", scope.IsAdmin ? "/admin/support" : "/business/support",
                db.SupportTickets.AsNoTracking().Where(x => !x.IsDeleted && (scope.IsAdmin || (x.UserId == scope.UserId && x.BusinessProfileId == businessId)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Subject, Reference = x.Reference, Context = "Support ticket", Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(SupportTicketStatus)));
        if ((scope.IsAdmin && scope.HasRole("Support", "Operations", "Admin", "SuperAdmin")) || !scope.IsAdmin)
            rows.Add(new SearchRecordSource("disputes", "Transfer disputes", scope.IsAdmin ? "/admin/support" : "/business/support",
                db.TransferDisputes.AsNoTracking().Where(x => !x.IsDeleted && (scope.IsAdmin || (x.UserId == scope.UserId && x.BusinessProfileId == businessId)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.Transfer.Reference + " " + (x.CurrencyCode ?? ""), Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(TransferDisputeStatus)));
        if (scope.Can(BusinessPermission.ViewTrading))
            rows.Add(new SearchRecordSource("orders", "Marketplace orders", "/business/trading",
                db.TradeOrders.AsNoTracking().Where(x => !x.IsDeleted && x.OwnerType == FinancialAccountOwnerType.Business && x.OwnerId == businessId)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.MarketplacePair.Code, Amount = null, AssetCode = x.MarketplacePair.BaseAssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(TradeOrderStatus)));
        if (scope.Can(BusinessPermission.ViewTrading))
            rows.Add(new SearchRecordSource("trades", "Marketplace trades", "/business/trading",
                db.Trades.AsNoTracking().Where(x => !x.IsDeleted && ((x.BuyerBaseFinancialAccount.OwnerType == FinancialAccountOwnerType.Business && x.BuyerBaseFinancialAccount.OwnerId == businessId) || (x.SellerBaseFinancialAccount.OwnerType == FinancialAccountOwnerType.Business && x.SellerBaseFinancialAccount.OwnerId == businessId)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.MarketplacePair.Code, Amount = null, AssetCode = x.MarketplacePair.BaseAssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(TradeStatus)));
        if ((scope.IsAdmin && scope.HasRole("Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewTrading))
            rows.Add(new SearchRecordSource("conversions", "Instant conversions", scope.IsAdmin ? "/admin/trading-wallets" : "/business/trading",
                db.InstantTrades.AsNoTracking().Where(x => !x.IsDeleted && (scope.IsAdmin || (x.OwnerType == FinancialAccountOwnerType.Business && x.OwnerId == businessId)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.InstantPair.Code, Amount = x.SourceAmount, AssetCode = x.InstantPair.SourceAssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(InstantTradeStatus)));
        if (scope.Can(BusinessPermission.ViewTrading))
            rows.Add(new SearchRecordSource("rfqs", "Business RFQs", "/business/trading",
                db.BusinessTradingRfqs.AsNoTracking().Where(x => !x.IsDeleted && ((x.RequesterOwnerType == FinancialAccountOwnerType.Business && x.RequesterOwnerId == businessId) || (x.CounterpartyOwnerType == FinancialAccountOwnerType.Business && x.CounterpartyOwnerId == businessId)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.MarketplacePair.Code, Amount = null, AssetCode = x.MarketplacePair.BaseAssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(BusinessTradingRfqStatus)));
        if ((scope.IsAdmin && scope.HasRole("Compliance", "Operations", "Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewDigitalAssets))
            rows.Add(new SearchRecordSource("digital-deposits", "Digital deposits", scope.IsAdmin ? "/admin/digital-assets" : "/business/digital-assets",
                db.DigitalAssetDepositIntents.AsNoTracking().Where(x => !x.IsDeleted && (scope.IsAdmin || (x.BusinessProfileId == businessId && x.BusinessCustomerId == null)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.AssetCode + " deposit", Reference = x.ProviderReference ?? "", Context = x.AssetCode + " " + x.NetworkCode, Amount = x.Amount, AssetCode = x.AssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(CollectionStatus)));
        if ((scope.IsAdmin && scope.HasRole("Compliance", "Operations", "Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewDigitalAssets))
            rows.Add(new SearchRecordSource("digital-withdrawals", "Digital withdrawals", scope.IsAdmin ? "/admin/digital-assets" : "/business/digital-assets",
                db.DigitalAssetWithdrawals.AsNoTracking().Where(x => !x.IsDeleted && (scope.IsAdmin || (x.BusinessProfileId == businessId && x.BusinessCustomerId == null)))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.AssetCode + " withdrawal", Reference = x.Payout.Reference, Context = x.AssetCode + " " + x.AssetNetwork.NetworkCode, Amount = x.Amount, AssetCode = x.AssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(DigitalAssetWithdrawalStatus)));
        if ((scope.IsAdmin && scope.HasRole("Compliance", "InternalAudit", "Operations", "Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewEmbeddedFinance))
            rows.Add(new SearchRecordSource("customers", "Business customers", scope.IsAdmin ? "/admin/embedded-finance" : "/business/customer-accounts",
                db.BusinessCustomers.AsNoTracking().Where(x => !x.IsDeleted && !x.BusinessProfile.IsDeleted && (scope.IsAdmin || x.BusinessProfileId == businessId))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.DisplayName, Reference = x.ExternalReference, Context = x.CountryCode, Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(BusinessCustomerStatus)));
        if ((scope.IsAdmin && scope.HasRole("Compliance", "InternalAudit", "Operations", "Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ViewEmbeddedFinance))
            rows.Add(new SearchRecordSource("collection-accounts", "Customer accounts", scope.IsAdmin ? "/admin/embedded-finance" : "/business/customer-accounts",
                db.CollectionAccounts.AsNoTracking().Where(x => !x.IsDeleted && !x.BusinessCustomer.IsDeleted && !x.BusinessProfile.IsDeleted && (scope.IsAdmin || x.BusinessProfileId == businessId))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.BusinessCustomer.DisplayName + " · " + x.AssetCode, Reference = x.ExternalReference, Context = x.AssetCode + " customer account", Amount = null, AssetCode = x.AssetCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(CollectionAccountStatus)));
        if ((scope.IsAdmin && scope.HasRole("Compliance", "InternalAudit", "Operations", "Admin", "SuperAdmin")) || scope.Can(BusinessPermission.ManageApiAccess))
            rows.Add(new SearchRecordSource("applications", "API applications", scope.IsAdmin ? "/admin/embedded-finance" : "/business/embedded-finance",
                db.ApiApplications.AsNoTracking().Where(x => !x.IsDeleted && !x.BusinessProfile.IsDeleted && (scope.IsAdmin || x.BusinessProfileId == businessId))
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Name, Reference = "", Context = "API application", Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(ApiApplicationStatus)));
        if (scope.IsAdmin && scope.HasRole("Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("users", "Users", "/admin/users",
                db.Users.AsNoTracking().Where(x => true)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.FirstName + " " + x.LastName, Reference = x.Email ?? "", Context = x.CountryCode ?? "", Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(UserStatus)));
        if (scope.IsAdmin && scope.HasRole("Compliance", "InternalAudit", "Operations", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("businesses", "Businesses", "/admin/embedded-finance",
                db.BusinessProfiles.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.BusinessName, Reference = "", Context = (x.TradingName ?? "") + " " + x.CountryCode, Amount = null, AssetCode = "", StateNumber = (int)x.KybStatus, Key = x.Id.ToString(), StateText = "" }),
                typeof(KybStatus)));
        if (scope.IsAdmin && scope.HasRole("Compliance", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("cases", "Compliance cases", "/admin/compliance",
                db.ComplianceCases.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Title, Reference = x.Reference, Context = "Compliance case", Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(ComplianceCaseStatus)));
        if (scope.IsAdmin && scope.HasRole("Compliance", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("kyb", "Business verification applications", "/admin/compliance",
                db.BusinessKybApplications.AsNoTracking().Where(x => !x.IsDeleted && !x.BusinessProfile.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.BusinessProfile.BusinessName, Reference = "", Context = x.BusinessProfile.CountryCode + " business verification", Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(KybStatus)));
        if (scope.IsAdmin && scope.HasRole("Compliance", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("regulatory-reports", "Regulatory reports", "/admin/compliance",
                db.RegulatoryReports.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.JurisdictionCode + " " + (x.RegulatoryAuthority ?? ""), Amount = null, AssetCode = "", StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(RegulatoryReportStatus)));
        if (scope.IsAdmin && scope.HasRole("Compliance", "InternalAudit", "Operations", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("restrictions", "Outbound restrictions", "/admin/outbound-restrictions",
                db.OutboundFundsRestrictions.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.SubjectDisplayName, Reference = x.ExternalReference ?? "", Context = "Outbound funds restriction", Amount = null, AssetCode = "", StateNumber = null, StateText = x.IsActive ? "Active" : "Released", Key = x.Id.ToString() }),
                null));
        if (scope.IsAdmin && scope.HasRole("SuperAdmin"))
            rows.Add(new SearchRecordSource("provider-wallets", "Provider wallets", "/admin/provider-wallets",
                db.ProviderWalletConfigurations.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.DisplayName, Reference = x.ProviderWalletId, Context = x.ProviderCode + " " + x.Environment + " " + x.AssetCode + " " + x.NetworkCode, Amount = null, AssetCode = x.AssetCode, StateNumber = null, StateText = x.IsActive ? "Active" : "Inactive", Key = x.Id.ToString() }),
                null));
        if (scope.IsAdmin && scope.HasRole("Operations", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("provider-transactions", "Provider transactions", "/admin/providers",
                db.ProviderTransactions.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.ProviderTransactionId, Reference = x.ProviderReference ?? x.ProviderTransactionId, Context = x.TransactionType + " " + x.CurrencyCode, Amount = x.Amount, AssetCode = x.CurrencyCode, StateNumber = null, StateText = x.ProviderStatus, Key = x.Id.ToString() }),
                null));
        if (scope.IsAdmin && scope.HasRole("Operations", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("settlements", "Settlement batches", "/admin/treasury/settlements",
                db.SettlementBatches.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.ProviderCode + " " + x.CurrencyCode, Amount = x.ExpectedNetAmount, AssetCode = x.CurrencyCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(SettlementBatchStatus)));
        if (scope.IsAdmin && scope.HasRole("Operations", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("rebalancing", "Treasury rebalancing", "/admin/treasury/rebalancing",
                db.TreasuryRebalanceRequests.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.FromCurrencyCode + " to " + x.ToCurrencyCode, Amount = x.RequestedAmount, AssetCode = x.FromCurrencyCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(TreasuryRebalanceStatus)));
        if (scope.IsAdmin && scope.HasRole("Operations", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("journals", "Accounting journals", "/admin/finance",
                db.JournalEntries.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.Reference, Reference = x.Reference, Context = x.CurrencyCode + " journal", Amount = null, AssetCode = x.CurrencyCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(JournalEntryStatus)));
        if (scope.IsAdmin && scope.HasRole("Operations", "Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("invoices", "Provider invoices", "/admin/finance",
                db.ProviderInvoices.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new SearchRecordRow { Id = x.Id, CreatedAt = x.CreatedAt, Title = x.InvoiceNumber, Reference = x.InvoiceNumber, Context = x.CurrencyCode + " provider invoice", Amount = x.TotalAmount, AssetCode = x.CurrencyCode, StateNumber = (int)x.Status, Key = x.Id.ToString(), StateText = "" }),
                typeof(ProviderInvoiceStatus)));
        if (scope.IsAdmin && scope.HasRole("Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("assets", "Currencies and crypto assets", "/admin/master-data",
                db.Assets.AsNoTracking().Where(x => true)
                    .Select(x => new SearchRecordRow { Id = Guid.Empty, CreatedAt = x.CreatedAt, Title = x.Name, Reference = x.Code, Context = x.Symbol + (x.Type == AssetType.Crypto ? " crypto digital asset" : " fiat currency"), Amount = null, AssetCode = x.Code, StateNumber = null, StateText = x.IsSupported ? "Supported" : "Disabled", Key = x.Code }),
                null));
        if (scope.IsAdmin && scope.HasRole("Admin", "SuperAdmin"))
            rows.Add(new SearchRecordSource("countries", "Countries", "/admin/master-data",
                db.Countries.AsNoTracking().Where(x => true)
                    .Select(x => new SearchRecordRow { Id = Guid.Empty, CreatedAt = x.CreatedAt, Title = x.Name, Reference = x.Code, Context = x.Iso3Code ?? "", Amount = null, AssetCode = "", StateNumber = null, StateText = x.IsSupported ? "Supported" : "Disabled", Key = x.Code }),
                null));
        return rows;
    }
}
