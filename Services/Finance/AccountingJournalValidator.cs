namespace KorridorX.Services.Finance;

public static class AccountingJournalValidator
{
    public static void Validate(IReadOnlyCollection<(decimal Debit, decimal Credit)> lines)
    {
        if (lines.Count < 2)
            throw new InvalidOperationException("A journal entry requires at least two lines.");

        if (lines.Any(x => x.Debit < 0 || x.Credit < 0 || (x.Debit > 0 && x.Credit > 0) || (x.Debit == 0 && x.Credit == 0)))
            throw new InvalidOperationException("Each journal line must contain either a positive debit or a positive credit, but not both.");

        var debit = decimal.Round(lines.Sum(x => x.Debit), 2, MidpointRounding.AwayFromZero);
        var credit = decimal.Round(lines.Sum(x => x.Credit), 2, MidpointRounding.AwayFromZero);
        if (debit != credit)
            throw new InvalidOperationException($"Journal entry is not balanced. Debits={debit:0.00}, Credits={credit:0.00}.");
    }
}
