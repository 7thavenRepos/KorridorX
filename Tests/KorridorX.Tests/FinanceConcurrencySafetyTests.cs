using System.Security.Cryptography;
using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.Finance;
using KorridorX.Models.Enums;
using KorridorX.Models.Finance;
using KorridorX.Services.Finance;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class FinanceConcurrencySafetyTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public FinanceConcurrencySafetyTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_same_provider_invoice_number_can_commit_only_once()
    {
        var invoiceNumber = $"P15B-NO-{Guid.NewGuid():N}";
        var gate = NewGate();

        async Task<(bool Success, Exception? Error)> ImportAsync(
            string fileName,
            string description)
        {
            await gate.Task;

            await using var scope = _fixture.Factory.Services.CreateAsyncScope();
            var service =
                scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

            try
            {
                await service.ImportProviderInvoiceAsync(
                    Guid.NewGuid(),
                    InvoiceRequest(
                        invoiceNumber,
                        TextFile(
                            fileName,
                            ValidInvoiceCsv(description))));
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        var first = ImportAsync(
            "invoice-number-a.csv",
            "Provider fee A");
        var second = ImportAsync(
            "invoice-number-b.csv",
            "Provider fee B");

        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(x => x.Success));

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.ProviderInvoices
            .AsNoTracking()
            .Where(x =>
                x.ProviderCode == ProviderCode.Blaaiz &&
                x.InvoiceNumber == invoiceNumber &&
                !x.IsDeleted)
            .ToListAsync();

        Assert.Single(rows);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_same_provider_invoice_file_hash_can_commit_only_once()
    {
        var unique = Guid.NewGuid().ToString("N");
        var csv = ValidInvoiceCsv(
            $"Identical provider file {unique}");
        var bytes = Encoding.UTF8.GetBytes(csv);
        var expectedHash =
            Convert.ToHexString(SHA256.HashData(bytes));

        var gate = NewGate();

        async Task<(bool Success, Exception? Error)> ImportAsync(
            string invoiceNumber,
            string fileName)
        {
            await gate.Task;

            await using var scope = _fixture.Factory.Services.CreateAsyncScope();
            var service =
                scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

            try
            {
                await service.ImportProviderInvoiceAsync(
                    Guid.NewGuid(),
                    InvoiceRequest(
                        invoiceNumber,
                        TextFile(fileName, csv)));
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        var first = ImportAsync(
            $"P15B-HASH-A-{Guid.NewGuid():N}",
            "same-hash-a.csv");
        var second = ImportAsync(
            $"P15B-HASH-B-{Guid.NewGuid():N}",
            "same-hash-b.csv");

        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(x => x.Success));

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.ProviderInvoices
            .AsNoTracking()
            .Where(x =>
                x.ProviderCode == ProviderCode.Blaaiz &&
                x.FileHash == expectedHash &&
                !x.IsDeleted)
            .ToListAsync();

        Assert.Single(rows);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_journal_reversal_can_create_only_one_reversal()
    {
        Guid originalId;

        await using (var setupScope =
            _fixture.Factory.Services.CreateAsyncScope())
        {
            var db =
                setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var unique = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;

            var period = new AccountingPeriod
            {
                Name = $"P15B-{unique[..12]}",
                StartsAt = now.Date.AddDays(-7),
                EndsAt = now.Date.AddDays(7),
                Status = AccountingPeriodStatus.Open
            };

            var debitAccount = new AccountingAccount
            {
                Code = $"D{unique[..10]}",
                Name = "Phase 15B Debit",
                Type = AccountingAccountType.Asset,
                IsActive = true
            };

            var creditAccount = new AccountingAccount
            {
                Code = $"C{unique[..10]}",
                Name = "Phase 15B Credit",
                Type = AccountingAccountType.Liability,
                IsActive = true
            };

            var original = new JournalEntry
            {
                Reference = $"JE-P15B-{unique[..12].ToUpperInvariant()}",
                SourceKey = $"PHASE15B:{unique}",
                SourceType = AccountingSourceType.ManualAdjustment,
                AccountingPeriod = period,
                AccountingPeriodId = period.Id,
                EntryDate = now,
                CurrencyCode = "CAD",
                Description = "Phase 15B concurrent reversal source",
                Status = JournalEntryStatus.Posted
            };

            original.Lines.Add(
                new JournalLine
                {
                    AccountingAccount = debitAccount,
                    AccountingAccountId = debitAccount.Id,
                    DebitAmount = 100m,
                    CreditAmount = 0m,
                    Narrative = "Debit"
                });

            original.Lines.Add(
                new JournalLine
                {
                    AccountingAccount = creditAccount,
                    AccountingAccountId = creditAccount.Id,
                    DebitAmount = 0m,
                    CreditAmount = 100m,
                    Narrative = "Credit"
                });

            db.AccountingPeriods.Add(period);
            db.AccountingAccounts.AddRange(
                debitAccount,
                creditAccount);
            db.JournalEntries.Add(original);

            await db.SaveChangesAsync();

            originalId = original.Id;
        }

        var gate = NewGate();

        async Task<(bool Success, Guid? ReversalId, Exception? Error)>
            ReverseAsync()
        {
            await gate.Task;

            await using var scope =
                _fixture.Factory.Services.CreateAsyncScope();
            var service =
                scope.ServiceProvider.GetRequiredService<IAccountingService>();

            try
            {
                var result = await service.ReverseJournalAsync(
                    Guid.NewGuid(),
                    originalId,
                    new ReverseJournalRequest
                    {
                        EntryDate = DateTime.UtcNow,
                        Reason = "Concurrent reversal test."
                    });

                return (true, result.Id, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex);
            }
        }

        var first = ReverseAsync();
        var second = ReverseAsync();

        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(x => x.Success));

        await using var verifyScope =
            _fixture.Factory.Services.CreateAsyncScope();
        var verifyDb =
            verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var originalAfter = await verifyDb.JournalEntries
            .AsNoTracking()
            .SingleAsync(x => x.Id == originalId);

        Assert.Equal(
            JournalEntryStatus.Reversed,
            originalAfter.Status);
        Assert.NotNull(originalAfter.ReversedAt);
        Assert.NotNull(originalAfter.ReversedByUserId);

        var reversals = await verifyDb.JournalEntries
            .AsNoTracking()
            .Where(x =>
                x.ReversalOfJournalEntryId == originalId &&
                !x.IsDeleted)
            .ToListAsync();

        Assert.Single(reversals);
        Assert.Equal(
            $"REVERSAL:{originalId}",
            reversals[0].SourceKey);

        var successfulReversalId = results
            .Where(x => x.Success)
            .Select(x => x.ReversalId)
            .Single();

        Assert.Equal(
            reversals[0].Id,
            successfulReversalId);
    }

    private static TaskCompletionSource NewGate() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ImportProviderInvoiceFormDto InvoiceRequest(
        string invoiceNumber,
        IFormFile file)
    {
        var now = DateTime.UtcNow;

        return new ImportProviderInvoiceFormDto
        {
            ProviderCode = ProviderCode.Blaaiz,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = now,
            PeriodStart = now.AddDays(-7),
            PeriodEnd = now,
            CurrencyCode = "CAD",
            File = file
        };
    }

    private static string ValidInvoiceCsv(
        string description) =>
        "description,net_amount,tax_amount,total_amount\n" +
        $"{description},10.00,1.30,11.30";

    private static IFormFile TextFile(
        string fileName,
        string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(
            stream,
            0,
            bytes.Length,
            "File",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }
}
