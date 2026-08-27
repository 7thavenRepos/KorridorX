using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Auth;

public sealed record TransactionPinStatusDto(
    bool IsConfigured,
    int MinimumLength,
    int MaximumLength,
    DateTime? LockedUntil,
    DateTime? UpdatedAt);

public sealed record SetTransactionPinRequestDto(
    [param: Required, MaxLength(256)] string CurrentPassword,
    [param: Required, MinLength(4), MaxLength(6)] string Pin);
