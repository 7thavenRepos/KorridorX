namespace KorridorX.Dtos.Finance;

public sealed class FinanceSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int CompletedTransferCount { get; set; }
    public int RefundedTransferCount { get; set; }
    public int FailedTransferCount { get; set; }
    public List<FinanceCurrencyAmountDto> SourceVolumes { get; set; } = new();
    public List<FinanceCurrencyAmountDto> FeeRevenue { get; set; } = new();
    public List<FinanceCorridorDto> Corridors { get; set; } = new();
    public decimal SettlementVarianceAbsoluteTotal { get; set; }
}

public sealed class FinanceCurrencyAmountDto
{
    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class FinanceCorridorDto
{
    public string SourceCurrencyCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public int TransferCount { get; set; }
    public decimal SourceVolume { get; set; }
    public decimal DestinationVolume { get; set; }
    public decimal FeeRevenue { get; set; }
    public string FeeCurrencyCode { get; set; } = "";
    public decimal IndicativeSpreadValue { get; set; }
    public string IndicativeSpreadCurrencyCode { get; set; } = "";
}
