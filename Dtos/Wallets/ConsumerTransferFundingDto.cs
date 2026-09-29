using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Wallets;

public sealed record ConsumerTransferFundingDto(
    Guid TransferId,
    Guid FinancialAccountId,
    string AssetCode,
    decimal TotalPayableAmount,
    decimal ReservedAmount,
    decimal ExternalFundingRequired,
    FinancialReservationStatus? ReservationStatus,
    bool IsFullyFunded);
