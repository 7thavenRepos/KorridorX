using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public sealed record EmbeddedTradingBalanceDto(
    Guid FinancialAccountId,
    string AssetCode,
    FinancialAccountStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance);
