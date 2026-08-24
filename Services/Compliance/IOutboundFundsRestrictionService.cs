using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public interface IOutboundFundsRestrictionService
{
    Task EnsureUserOutboundAllowedAsync(
        Guid userId,
        string operation,
        CancellationToken ct = default);

    Task EnsureBusinessOutboundAllowedAsync(
        Guid businessProfileId,
        string operation,
        CancellationToken ct = default);

    Task EnsureBusinessCustomerOutboundAllowedAsync(
        Guid businessCustomerId,
        string operation,
        CancellationToken ct = default);

    Task EnsureFinancialAccountOwnerOutboundAllowedAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        string operation,
        CancellationToken ct = default);

    Task<IReadOnlyList<OutboundFundsRestrictionSubjectLookupDto>> SearchSubjectsAsync(
        OutboundFundsRestrictionSubjectType subjectType,
        string search,
        int take = 20,
        CancellationToken ct = default);

    Task<PagedResult<OutboundFundsRestrictionDto>> GetAsync(
        OutboundFundsRestrictionSubjectType? subjectType,
        OutboundFundsRestrictionSource? source,
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<OutboundFundsRestrictionDto> GetByIdAsync(
        Guid restrictionId,
        CancellationToken ct = default);

    Task<OutboundFundsRestrictionDto> ApplyAsync(
        ApplyOutboundFundsRestrictionRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<OutboundFundsRestrictionDto> LiftAsync(
        Guid restrictionId,
        LiftOutboundFundsRestrictionRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default);
}
