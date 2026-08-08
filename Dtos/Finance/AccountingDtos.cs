using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Finance;

public sealed class AccountingAccountDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AccountingAccountType Type { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}

public sealed class CreateAccountingAccountRequest
{
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [Required, MaxLength(150)] public string Name { get; set; } = "";
    public AccountingAccountType Type { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
}

public sealed class AccountingPeriodDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AccountingPeriodStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? CloseNote { get; set; }
}

public sealed class CreateAccountingPeriodRequest
{
    [Required, MaxLength(50)] public string Name { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
}

public sealed class CloseAccountingPeriodRequest
{
    [MaxLength(2000)] public string? Note { get; set; }
}

public sealed class ManualJournalLineRequest
{
    public Guid AccountingAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    [MaxLength(1000)] public string? Narrative { get; set; }
}

public sealed class CreateManualJournalRequest
{
    public DateTime EntryDate { get; set; }
    [Required, MaxLength(10)] public string CurrencyCode { get; set; } = "";
    [Required, MaxLength(1000)] public string Description { get; set; } = "";
    [MinLength(2)] public List<ManualJournalLineRequest> Lines { get; set; } = new();
}


public sealed class ReverseJournalRequest
{
    public DateTime? EntryDate { get; set; }
    [Required, MaxLength(2000)] public string Reason { get; set; } = "";
}

public sealed class JournalLineDto
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; } = "";
    public string AccountName { get; set; } = "";
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Narrative { get; set; }
}

public sealed class JournalEntryDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public AccountingSourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string PeriodName { get; set; } = "";
    public DateTime EntryDate { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string Description { get; set; } = "";
    public JournalEntryStatus Status { get; set; }
    public List<JournalLineDto> Lines { get; set; } = new();
}

public sealed class TrialBalanceRowDto
{
    public string AccountCode { get; set; } = "";
    public string AccountName { get; set; } = "";
    public AccountingAccountType AccountType { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    public decimal Balance { get; set; }
}

public sealed class AccountingSyncResultDto
{
    public int TransferFeeEntries { get; set; }
    public int FxSpreadEntries { get; set; }
    public int ProviderFeeEntries { get; set; }
    public int SettlementVarianceEntries { get; set; }
    public int RefundReversalEntries { get; set; }
    public int AlreadyPosted { get; set; }
    public int SkippedClosedPeriod { get; set; }
}
