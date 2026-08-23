using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.Finance;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.Finance;
using KorridorX.Models.Treasury;
using KorridorX.Services.Finance;
using KorridorX.Services.Treasury;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class FinancialAdminSecurityWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public FinancialAdminSecurityWorkflowTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(typeof(AdminTreasuryController))]
    [InlineData(typeof(AdminFxOperationsController))]
    [InlineData(typeof(AdminFinanceController))]
    [InlineData(typeof(AdminAccountingController))]
    [InlineData(typeof(AdminFinancialCloseController))]
    public void Financial_admin_controllers_require_operations_admin_or_superadmin(
        Type controllerType)
    {
        var authorize = controllerType
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();

        Assert.NotEmpty(authorize);

        var roleSets = authorize
            .Where(x => !string.IsNullOrWhiteSpace(x.Roles))
            .SelectMany(x => x.Roles!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Operations", roleSets);
        Assert.Contains("Admin", roleSets);
        Assert.Contains("SuperAdmin", roleSets);
    }

    [Theory]
    [InlineData(nameof(AdminAccountingController.Close))]
    [InlineData(nameof(AdminAccountingController.Reopen))]
    public void Direct_accounting_period_state_changes_are_superadmin_only(
        string methodName)
    {
        var method = typeof(AdminAccountingController)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException(
                $"AdminAccountingController.{methodName} was not found.");

        var authorize = method
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();

        Assert.Contains(
            authorize,
            x => string.Equals(
                x.Roles,
                "SuperAdmin",
                StringComparison.OrdinalIgnoreCase));
    }

    [DatabaseIntegrationFact]
    public Task Treasury_rebalance_requester_cannot_approve_their_own_request()
        => Treasury_rebalance_requester_cannot_review_their_own_request(approve: true);

    [DatabaseIntegrationFact]
    public Task Treasury_rebalance_requester_cannot_reject_their_own_request()
        => Treasury_rebalance_requester_cannot_review_their_own_request(approve: false);

    private async Task Treasury_rebalance_requester_cannot_review_their_own_request(
        bool approve)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var requester = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var fromWallet = new ProviderWalletBalance
        {
            ProviderCode = "Blaaiz",
            ProviderWalletId = $"phase15a-from-{suffix}",
            ProviderBusinessId = $"phase15a-business-{suffix}",
            CurrencyCode = "CAD",
            Balance = 1000m,
            IsActive = true,
            LastSyncedAt = DateTime.UtcNow
        };

        var toWallet = new ProviderWalletBalance
        {
            ProviderCode = "Blaaiz",
            ProviderWalletId = $"phase15a-to-{suffix}",
            ProviderBusinessId = $"phase15a-business-{suffix}",
            CurrencyCode = "USD",
            Balance = 500m,
            IsActive = true,
            LastSyncedAt = DateTime.UtcNow
        };

        var rebalance = new TreasuryRebalanceRequest
        {
            Reference = $"TRB-P15A-{suffix[..12].ToUpperInvariant()}",
            ProviderCode = "Blaaiz",
            FromProviderWalletBalance = fromWallet,
            ToProviderWalletBalance = toWallet,
            FromProviderWalletBalanceId = fromWallet.Id,
            ToProviderWalletBalanceId = toWallet.Id,
            FromCurrencyCode = "CAD",
            ToCurrencyCode = "USD",
            RequestedAmount = 100m,
            AmountType = TreasurySwapAmountType.From,
            Status = TreasuryRebalanceStatus.PendingApproval,
            RequestedByUserId = requester,
            CreatedByUserId = requester
        };

        db.ProviderWalletBalances.AddRange(fromWallet, toWallet);
        db.TreasuryRebalanceRequests.Add(rebalance);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            treasury.ReviewRebalanceAsync(
                requester,
                rebalance.Id,
                new ReviewTreasuryRebalanceRequestDto
                {
                    Approve = approve,
                    Note = approve ? "Approve" : "Reject"
                }));

        Assert.Contains(
            "cannot approve or reject the same request",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();

        var stored = await db.TreasuryRebalanceRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == rebalance.Id);

        Assert.Equal(TreasuryRebalanceStatus.PendingApproval, stored.Status);
        Assert.Null(stored.ApprovedByUserId);
        Assert.Null(stored.RejectedByUserId);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_invoice_importer_cannot_approve_their_own_invoice()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var finance = scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

        var importer = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        var invoice = new ProviderInvoice
        {
            ProviderCode = ProviderCode.Blaaiz,
            InvoiceNumber = $"P15A-{suffix[..16].ToUpperInvariant()}",
            InvoiceDate = now,
            PeriodStart = now.Date.AddDays(-1),
            PeriodEnd = now.Date.AddDays(1),
            CurrencyCode = "CAD",
            NetAmount = 100m,
            TaxAmount = 0m,
            TotalAmount = 100m,
            MatchedProviderFeeAmount = 100m,
            VarianceAmount = 0m,
            Status = ProviderInvoiceStatus.Matched,
            FileName = $"phase15a-{suffix}.csv",
            FileHash = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            ImportedByUserId = importer,
            ImportedAt = now,
            CreatedByUserId = importer
        };

        db.ProviderInvoices.Add(invoice);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.ReviewProviderInvoiceAsync(
                importer,
                invoice.Id,
                new ReviewProviderInvoiceRequest
                {
                    Approve = true,
                    Note = "Approve own import"
                }));

        Assert.Contains(
            "imported a provider invoice cannot approve it",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();

        var stored = await db.ProviderInvoices
            .AsNoTracking()
            .SingleAsync(x => x.Id == invoice.Id);

        Assert.Equal(ProviderInvoiceStatus.Matched, stored.Status);
        Assert.Null(stored.ReviewedByUserId);
        Assert.Null(stored.PostedAt);
    }

    [DatabaseIntegrationFact]
    public async Task Finance_close_requester_cannot_approve_their_own_close()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var finance = scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

        var requester = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var monthStart = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var period = new AccountingPeriod
        {
            Name = $"Phase15A-{suffix[..12]}",
            StartsAt = monthStart.AddMonths(-2),
            EndsAt = monthStart.AddMonths(-1),
            Status = AccountingPeriodStatus.Open,
            CreatedByUserId = requester
        };

        var closeRequest = new FinanceCloseRequest
        {
            AccountingPeriod = period,
            AccountingPeriodId = period.Id,
            Status = FinanceCloseRequestStatus.PendingApproval,
            RequestedByUserId = requester,
            RequestedAt = DateTime.UtcNow,
            RequestNote = "Phase 15A maker/checker validation",
            CreatedByUserId = requester
        };

        db.AccountingPeriods.Add(period);
        db.FinanceCloseRequests.Add(closeRequest);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.ReviewCloseRequestAsync(
                requester,
                closeRequest.Id,
                new ReviewFinanceCloseRequest
                {
                    Approve = true,
                    Note = "Approve own close request"
                }));

        Assert.Contains(
            "requested period close cannot approve it",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();

        var stored = await db.FinanceCloseRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == closeRequest.Id);

        Assert.Equal(FinanceCloseRequestStatus.PendingApproval, stored.Status);
        Assert.Null(stored.ReviewedByUserId);
        Assert.Null(stored.ExecutedAt);

        var storedPeriod = await db.AccountingPeriods
            .AsNoTracking()
            .SingleAsync(x => x.Id == period.Id);

        Assert.Equal(AccountingPeriodStatus.Open, storedPeriod.Status);
        Assert.Null(storedPeriod.ClosedAt);
    }
}
