using KorridorX.Models.Audit;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.BusinessTransfers;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Compliance;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Fx;
using KorridorX.Models.Finance;
using KorridorX.Models.Identity;
using KorridorX.Models.Instant;
using KorridorX.Models.Lookups;
using KorridorX.Models.Marketplace;
using KorridorX.Models.Notifications;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Recipients;
using KorridorX.Models.Support;
using KorridorX.Models.Transfers;
using KorridorX.Models.Treasury;
using KorridorX.Models.Webhooks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();

    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();

    public DbSet<ApiApplication> ApiApplications => Set<ApiApplication>();
    public DbSet<ApiCredential> ApiCredentials => Set<ApiCredential>();
    public DbSet<BusinessCustomer> BusinessCustomers => Set<BusinessCustomer>();
    public DbSet<CollectionAccount> CollectionAccounts => Set<CollectionAccount>();
    public DbSet<ProviderAccountMapping> ProviderAccountMappings => Set<ProviderAccountMapping>();
    public DbSet<EmbeddedApiIdempotencyRecord> EmbeddedApiIdempotencyRecords => Set<EmbeddedApiIdempotencyRecord>();

    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<RecipientBankAccount> RecipientBankAccounts => Set<RecipientBankAccount>();
    public DbSet<RecipientMobileWallet> RecipientMobileWallets => Set<RecipientMobileWallet>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<TransferQuote> TransferQuotes => Set<TransferQuote>();
    public DbSet<TransferFee> TransferFees => Set<TransferFee>();

    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<TransferStatusHistory> TransferStatusHistories => Set<TransferStatusHistory>();
    public DbSet<TransferTimelineEvent> TransferTimelineEvents => Set<TransferTimelineEvent>();
    public DbSet<BusinessPaymentBatch> BusinessPaymentBatches => Set<BusinessPaymentBatch>();
    public DbSet<BusinessPaymentBatchItem> BusinessPaymentBatchItems => Set<BusinessPaymentBatchItem>();
    public DbSet<BusinessApproval> BusinessApprovals => Set<BusinessApproval>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerPosting> LedgerPostings => Set<LedgerPosting>();
    public DbSet<FinancialReservation> FinancialReservations => Set<FinancialReservation>();

    public DbSet<MarketplacePair> MarketplacePairs => Set<MarketplacePair>();
    public DbSet<TradeOrder> TradeOrders => Set<TradeOrder>();
    public DbSet<TradeMatch> TradeMatches => Set<TradeMatch>();
    public DbSet<Trade> Trades => Set<Trade>();

    public DbSet<InstantPair> InstantPairs => Set<InstantPair>();
    public DbSet<InstantQuote> InstantQuotes => Set<InstantQuote>();
    public DbSet<InstantTrade> InstantTrades => Set<InstantTrade>();

    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionAttempt> CollectionAttempts => Set<CollectionAttempt>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<PayoutAttempt> PayoutAttempts => Set<PayoutAttempt>();

    public DbSet<KycProfile> KycProfiles => Set<KycProfile>();
    public DbSet<KycApplication> KycApplications => Set<KycApplication>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<BusinessKybApplication> BusinessKybApplications => Set<BusinessKybApplication>();
    public DbSet<BusinessBeneficialOwner> BusinessBeneficialOwners => Set<BusinessBeneficialOwner>();
    public DbSet<BusinessKybDocument> BusinessKybDocuments => Set<BusinessKybDocument>();
    public DbSet<BusinessBeneficiary> BusinessBeneficiaries => Set<BusinessBeneficiary>();
    public DbSet<BusinessBeneficiaryBankAccount> BusinessBeneficiaryBankAccounts => Set<BusinessBeneficiaryBankAccount>();
    public DbSet<BusinessBeneficiaryMobileWallet> BusinessBeneficiaryMobileWallets => Set<BusinessBeneficiaryMobileWallet>();
    public DbSet<ComplianceLimit> ComplianceLimits => Set<ComplianceLimit>();
    public DbSet<ComplianceCheck> ComplianceChecks => Set<ComplianceCheck>();
    public DbSet<AmlFlag> AmlFlags => Set<AmlFlag>();
    public DbSet<ScreeningRecord> ScreeningRecords => Set<ScreeningRecord>();
    public DbSet<ScreeningMatch> ScreeningMatches => Set<ScreeningMatch>();
    public DbSet<ComplianceCase> ComplianceCases => Set<ComplianceCase>();
    public DbSet<ComplianceCaseNote> ComplianceCaseNotes => Set<ComplianceCaseNote>();
    public DbSet<ComplianceCaseEvidence> ComplianceCaseEvidence => Set<ComplianceCaseEvidence>();
    public DbSet<RegulatoryReport> RegulatoryReports => Set<RegulatoryReport>();
    public DbSet<DataRetentionPolicy> DataRetentionPolicies => Set<DataRetentionPolicy>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<RetentionExecutionLog> RetentionExecutionLogs => Set<RetentionExecutionLog>();

    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
    public DbSet<ProviderCustomer> ProviderCustomers => Set<ProviderCustomer>();
    public DbSet<ProviderRequestLog> ProviderRequestLogs => Set<ProviderRequestLog>();
    public DbSet<ProviderTransaction> ProviderTransactions => Set<ProviderTransaction>();
    public DbSet<ProviderBank> ProviderBanks => Set<ProviderBank>();
    public DbSet<PayoutDestinationProviderMapping> PayoutDestinationProviderMappings =>
        Set<PayoutDestinationProviderMapping>();
    public DbSet<AccountingAccount> AccountingAccounts => Set<AccountingAccount>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<FinanceTranslationRate> FinanceTranslationRates => Set<FinanceTranslationRate>();
    public DbSet<ProviderInvoice> ProviderInvoices => Set<ProviderInvoice>();
    public DbSet<ProviderInvoiceLine> ProviderInvoiceLines => Set<ProviderInvoiceLine>();
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();
    public DbSet<FinanceCloseChecklistItem> FinanceCloseChecklistItems => Set<FinanceCloseChecklistItem>();
    public DbSet<FinanceCloseRequest> FinanceCloseRequests => Set<FinanceCloseRequest>();

    public DbSet<ProviderWalletBalance> ProviderWalletBalances => Set<ProviderWalletBalance>();
    public DbSet<LiquidityThreshold> LiquidityThresholds => Set<LiquidityThreshold>();
    public DbSet<FxMarkupRule> FxMarkupRules => Set<FxMarkupRule>();
    public DbSet<SettlementBatch> SettlementBatches => Set<SettlementBatch>();
    public DbSet<SettlementBatchItem> SettlementBatchItems => Set<SettlementBatchItem>();
    public DbSet<TreasuryRebalanceRequest> TreasuryRebalanceRequests => Set<TreasuryRebalanceRequest>();
    public DbSet<SettlementStatementImport> SettlementStatementImports => Set<SettlementStatementImport>();
    public DbSet<SettlementStatementItem> SettlementStatementItems => Set<SettlementStatementItem>();

    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<WebhookProcessingAttempt> WebhookProcessingAttempts => Set<WebhookProcessingAttempt>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<TransferDispute> TransferDisputes => Set<TransferDispute>();
    public DbSet<TransferInvestigation> TransferInvestigations => Set<TransferInvestigation>();
    public DbSet<SupportEvidence> SupportEvidence => Set<SupportEvidence>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetNetwork> AssetNetworks => Set<AssetNetwork>();
    public DbSet<CountryAsset> CountryAssets => Set<CountryAsset>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}