using KorridorX.Services.Finance;

namespace KorridorX.Tests;

public sealed class AccountingJournalValidatorTests
{
    [Fact]
    public void Validate_AllowsBalancedJournal()
    {
        AccountingJournalValidator.Validate(new List<(decimal Debit, decimal Credit)>
        {
            (100m, 0m),
            (0m, 100m)
        });
    }

    [Fact]
    public void Validate_RejectsUnbalancedJournal()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => AccountingJournalValidator.Validate(new List<(decimal Debit, decimal Credit)>
        {
            (100m, 0m),
            (0m, 90m)
        }));
        Assert.Contains("not balanced", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsDebitAndCreditOnSameLine()
    {
        Assert.Throws<InvalidOperationException>(() => AccountingJournalValidator.Validate(new List<(decimal Debit, decimal Credit)>
        {
            (100m, 10m),
            (0m, 90m)
        }));
    }
}
