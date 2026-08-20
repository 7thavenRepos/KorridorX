using KorridorX.Dtos.BusinessFunding;
using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.BusinessFunding;

public interface IBusinessFundingService
{
    Task<PagedResult<BusinessWalletDto>> GetWalletsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<BusinessLedgerTransactionDto>> GetLedgerAsync(
        Guid userId,
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<BusinessWalletDto> CreditWalletAsync(
        Guid adminUserId,
        AdminCreditBusinessWalletRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<BusinessWalletDto> SetWalletStatusAsync(
        Guid adminUserId,
        Guid walletId,
        FinancialAccountStatus status,
        string reason,
        CancellationToken ct = default);

    Task<BusinessWalletDto> AdjustWalletAsync(
        Guid adminUserId,
        Guid walletId,
        AdminAdjustBusinessWalletRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<BusinessLedgerTransactionDto> ReverseLedgerTransactionAsync(
        Guid adminUserId,
        Guid transactionId,
        string reason,
        CancellationToken ct = default);

    Task<BusinessTransferFundingDto> GetTransferFundingAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default);

    Task<BusinessTransferFundingDto> FundTransferFromWalletAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> CreateExternalCollectionAsync(
        Guid userId,
        Guid transferId,
        CreateBusinessExternalCollectionRequestDto request,
        CancellationToken ct = default);

    Task<CollectionDetailsDto> InitiateExternalCollectionAsync(
        Guid userId,
        Guid collectionId,
        InitiateCollectionRequestDto request,
        CancellationToken ct = default);

    Task EnsureAvailableBalanceAsync(
        Guid businessProfileId,
        string currencyCode,
        decimal amount,
        CancellationToken ct = default);

    Task ReserveTransferAsync(
        Transfer transfer,
        Guid? actionedByUserId,
        string source,
        Guid? businessPaymentBatchId = null,
        CancellationToken ct = default);

    Task ReactivateTransferReservationAsync(
        Transfer transfer,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default);

    bool CaptureTransferReservation(
        Transfer transfer,
        Guid? actionedByUserId,
        string source);

    bool ReleaseTransferReservation(
        Transfer transfer,
        string reason,
        Guid? actionedByUserId,
        string source);
}
