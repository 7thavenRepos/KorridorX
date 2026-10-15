using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Auth;

public sealed record TransactionPinStatusDto(
    bool IsConfigured,
    int MinimumLength,
    int MaximumLength,
    DateTime? LockedUntil,
    DateTime? UpdatedAt,
    bool CanSetWithGoogle);

public sealed record SetTransactionPinRequestDto(
    [param: Required, MaxLength(256)] string CurrentPassword,
    [param: Required, MinLength(4), MaxLength(6)] string Pin);

// Google is a fresh signed account proof, never an alternative to the transaction PIN
// itself. The server verifies the linked subject and consumes this proof exactly once.
public sealed record SetGoogleTransactionPinRequestDto(
    [param: Required, MaxLength(4096)] string ChallengeToken,
    [param: Required, MaxLength(16384)] string IdToken,
    [param: Required, MinLength(4), MaxLength(6)] string Pin);
