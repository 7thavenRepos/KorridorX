using KorridorX.Dtos.Auth;

namespace KorridorX.Services.Security;

public interface ITransactionPinService
{
    Task<TransactionPinStatusDto> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<TransactionPinStatusDto> SetAsync(
        Guid userId,
        string currentPassword,
        string pin,
        CancellationToken ct = default);

    Task<ConsumerExternalChallengeDto> CreateGoogleChallengeAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<TransactionPinStatusDto> SetWithGoogleAsync(
        Guid userId,
        SetGoogleTransactionPinRequestDto request,
        CancellationToken ct = default);

    Task VerifyForTransactionAsync(
        Guid userId,
        string? pin,
        CancellationToken ct = default);
}
