using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Treasury;

public class LiquidityThreshold : AuditableEntity
{
    public TreasuryLiquidityScopeType ScopeType { get; set; } =
        TreasuryLiquidityScopeType.ProviderWallet;

    public string ProviderCode { get; set; } = "Blaaiz";
    public string CurrencyCode { get; set; } = "";
    public string? NetworkCode { get; set; }
    public FinancialAccountType? FinancialAccountType { get; set; }
    public decimal MinimumBalance { get; set; }
    public decimal TargetBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public bool IsActive { get; set; } = true;
}
