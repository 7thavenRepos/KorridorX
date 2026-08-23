using KorridorX.Data;
using KorridorX.Dtos.Finance;
using KorridorX.Models.Enums;
using KorridorX.Models.Finance;
using KorridorX.Services.Finance;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class FinanceCloseConcurrencySafetyTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public FinanceCloseConcurrencySafetyTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_close_submissions_create_only_one_pending_request()
    {
        Guid periodId;

        await using (var setupScope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var accounting =
                setupScope.ServiceProvider.GetRequiredService<IAccountingService>();
            var db =
                setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

            await accounting.GetAccountsAsync();

            var period = CreateFuturePeriod();
            db.AccountingPeriods.Add(period);

            AddCompleteChecklist(db, period.Id);

            await db.SaveChangesAsync();

            periodId = period.Id;
        }

        var gate = NewGate();

        async Task<(bool Success, Exception? Error)> SubmitAsync()
        {
            await gate.Task;

            await using var scope =
                _fixture.Factory.Services.CreateAsyncScope();

            var finance =
                scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

            try
            {
                await finance.SubmitCloseRequestAsync(
                    Guid.NewGuid(),
                    periodId,
                    new CreateFinanceCloseRequest
                    {
                        Note = "Concurrent finance-close submission."
                    });

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        var first = SubmitAsync();
        var second = SubmitAsync();

        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(x => x.Success));

        var loser = Assert.Single(results, x => !x.Success);
        var loserError =
            Assert.IsType<InvalidOperationException>(loser.Error);

        Assert.Contains(
            "already pending approval",
            loserError.Message,
            StringComparison.OrdinalIgnoreCase);

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();

        var verifyDb =
            verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await verifyDb.FinanceCloseRequests
            .AsNoTracking()
            .Where(x =>
                x.AccountingPeriodId == periodId &&
                x.Status == FinanceCloseRequestStatus.PendingApproval &&
                !x.IsDeleted)
            .ToListAsync();

        Assert.Single(pending);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_close_approvals_execute_only_once()
    {
        Guid periodId;
        Guid closeRequestId;
        var requester = Guid.NewGuid();

        await using (var setupScope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var accounting =
                setupScope.ServiceProvider.GetRequiredService<IAccountingService>();
            var db =
                setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

            await accounting.GetAccountsAsync();

            var period = CreateFuturePeriod();

            var closeRequest = new FinanceCloseRequest
            {
                AccountingPeriod = period,
                AccountingPeriodId = period.Id,
                Status = FinanceCloseRequestStatus.PendingApproval,
                RequestedByUserId = requester,
                RequestedAt = DateTime.UtcNow,
                RequestNote = "Concurrent review validation.",
                CreatedByUserId = requester
            };

            db.AccountingPeriods.Add(period);
            db.FinanceCloseRequests.Add(closeRequest);

            await db.SaveChangesAsync();

            periodId = period.Id;
            closeRequestId = closeRequest.Id;
        }

        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var gate = NewGate();

        async Task<(bool Success, Guid Reviewer, Exception? Error)> ApproveAsync(
            Guid reviewer)
        {
            await gate.Task;

            await using var scope =
                _fixture.Factory.Services.CreateAsyncScope();

            var finance =
                scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

            try
            {
                await finance.ReviewCloseRequestAsync(
                    reviewer,
                    closeRequestId,
                    new ReviewFinanceCloseRequest
                    {
                        Approve = true,
                        Note = $"Approved by {reviewer}."
                    });

                return (true, reviewer, null);
            }
            catch (Exception ex)
            {
                return (false, reviewer, ex);
            }
        }

        var first = ApproveAsync(reviewerA);
        var second = ApproveAsync(reviewerB);

        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(x => x.Success));

        var loser = Assert.Single(results, x => !x.Success);
        var loserError =
            Assert.IsType<InvalidOperationException>(loser.Error);

        Assert.Contains(
            "no longer pending approval",
            loserError.Message,
            StringComparison.OrdinalIgnoreCase);

        var winner = Assert.Single(results, x => x.Success);

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();

        var verifyDb =
            verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var request = await verifyDb.FinanceCloseRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == closeRequestId);

        Assert.Equal(
            FinanceCloseRequestStatus.Executed,
            request.Status);
        Assert.Equal(winner.Reviewer, request.ReviewedByUserId);
        Assert.NotNull(request.ReviewedAt);
        Assert.NotNull(request.ExecutedAt);

        var storedPeriod = await verifyDb.AccountingPeriods
            .AsNoTracking()
            .SingleAsync(x => x.Id == periodId);

        Assert.Equal(
            AccountingPeriodStatus.Closed,
            storedPeriod.Status);
        Assert.Equal(winner.Reviewer, storedPeriod.ClosedByUserId);
        Assert.NotNull(storedPeriod.ClosedAt);
    }

    private static AccountingPeriod CreateFuturePeriod()
    {
        var unique = Guid.NewGuid().ToString("N");
        var start = new DateTime(
            DateTime.UtcNow.Year + 10,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        return new AccountingPeriod
        {
            Name = $"P15B-CLOSE-{unique[..12]}",
            StartsAt = start,
            EndsAt = start.AddMonths(1),
            Status = AccountingPeriodStatus.Open
        };
    }

    private static void AddCompleteChecklist(
        AppDbContext db,
        Guid periodId)
    {
        var definitions = new[]
        {
            ("ACCOUNTING_SYNC", true, true, 10),
            ("TRIAL_BALANCE", true, true, 20),
            ("PROVIDER_INVOICES", true, true, 30),
            ("SETTLEMENT_VARIANCES", true, true, 40),
            ("TAX_REVIEW", true, false, 50),
            ("MANAGEMENT_REVIEW", true, false, 60)
        };

        foreach (var item in definitions)
        {
            db.FinanceCloseChecklistItems.Add(
                new FinanceCloseChecklistItem
                {
                    AccountingPeriodId = periodId,
                    Code = item.Item1,
                    Label = item.Item1,
                    IsRequired = true,
                    IsSystemCheck = item.Item3,
                    IsCompleted = item.Item2,
                    CompletedAt = DateTime.UtcNow,
                    SortOrder = item.Item4
                });
        }
    }

    private static TaskCompletionSource NewGate() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
