using KorridorX.Dtos.Finance;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Finance;

public interface IAccountingService
{
    Task<IReadOnlyList<AccountingAccountDto>> GetAccountsAsync(CancellationToken ct = default);
    Task<AccountingAccountDto> CreateAccountAsync(Guid userId, CreateAccountingAccountRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AccountingPeriodDto>> GetPeriodsAsync(CancellationToken ct = default);
    Task<AccountingPeriodDto> CreatePeriodAsync(Guid userId, CreateAccountingPeriodRequest request, CancellationToken ct = default);
    Task<AccountingPeriodDto> ClosePeriodAsync(Guid userId, Guid periodId, CloseAccountingPeriodRequest request, CancellationToken ct = default);
    Task<AccountingPeriodDto> ReopenPeriodAsync(Guid userId, Guid periodId, CancellationToken ct = default);
    Task<JournalEntryDto> CreateManualJournalAsync(Guid userId, CreateManualJournalRequest request, CancellationToken ct = default);
    Task<JournalEntryDto> ReverseJournalAsync(Guid userId, Guid journalEntryId, ReverseJournalRequest request, CancellationToken ct = default);
    Task<PagedResult<JournalEntryDto>> GetJournalsAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<AccountingSyncResultDto> SyncAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<byte[]> ExportJournalsCsvAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}
