using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.FinancialCore;

public class LedgerPosting : BaseEntity
{
    public Guid LedgerTransactionId { get; set; }
    public LedgerTransaction LedgerTransaction { get; set; } = null!;

    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;

    public LedgerBalanceBucket BalanceBucket { get; set; }
    public LedgerPostingSide Side { get; set; }
    public decimal Amount { get; set; }
    public decimal? AccountBalanceAfter { get; set; }
}
