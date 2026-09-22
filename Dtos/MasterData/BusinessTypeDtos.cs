using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.MasterData;

public sealed record BusinessTypeDto(string Code, string Name, bool IsActive, int SortOrder);
public sealed record SaveBusinessTypeRequest(
    [Required, RegularExpression("^[a-z][a-z0-9_]{0,63}$")] string Code,
    [Required, MaxLength(100)] string Name,
    bool IsActive,
    [Range(0, 10000)] int SortOrder);
