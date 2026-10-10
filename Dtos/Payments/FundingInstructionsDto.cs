namespace KorridorX.Dtos.Payments;

// Shared, additive display contract. Fields are provider-supplied receiving details;
// local collection IDs and provider transaction IDs are never payment references.
public sealed record FundingInstructionsDto(
    string Kind, string CurrencyCode, string Title, string Message, bool IsReady,
    IReadOnlyList<FundingInstructionFieldDto> Fields, IReadOnlyList<string> Steps,
    DateTime? ExpiresAt = null);

public sealed record FundingInstructionFieldDto(string Key, string Label, string Value, bool Copyable = true);
