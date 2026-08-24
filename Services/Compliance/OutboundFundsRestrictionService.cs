using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Compliance;
using KorridorX.Exceptions;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public sealed class OutboundFundsRestrictionService
    : IOutboundFundsRestrictionService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<OutboundFundsRestrictionService> _logger;

    public OutboundFundsRestrictionService(
        AppDbContext db,
        IAuditService audit,
        ILogger<OutboundFundsRestrictionService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task EnsureUserOutboundAllowedAsync(
        Guid userId,
        string operation,
        CancellationToken ct = default)
    {
        var restriction = await FindActiveAsync(
            OutboundFundsRestrictionSubjectType.User,
            userId,
            ct);

        ThrowIfRestricted(restriction, operation);
    }

    public async Task EnsureBusinessOutboundAllowedAsync(
        Guid businessProfileId,
        string operation,
        CancellationToken ct = default)
    {
        var restriction = await FindActiveAsync(
            OutboundFundsRestrictionSubjectType.BusinessProfile,
            businessProfileId,
            ct);

        ThrowIfRestricted(restriction, operation);
    }

    public async Task EnsureBusinessCustomerOutboundAllowedAsync(
        Guid businessCustomerId,
        string operation,
        CancellationToken ct = default)
    {
        var customer = await _db.BusinessCustomers
            .AsNoTracking()
            .Where(x => x.Id == businessCustomerId && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.BusinessProfileId
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Business customer not found.");

        var customerRestriction = await FindActiveAsync(
            OutboundFundsRestrictionSubjectType.BusinessCustomer,
            customer.Id,
            ct);

        ThrowIfRestricted(customerRestriction, operation);

        var businessRestriction = await FindActiveAsync(
            OutboundFundsRestrictionSubjectType.BusinessProfile,
            customer.BusinessProfileId,
            ct);

        ThrowIfRestricted(businessRestriction, operation);
    }

    public async Task EnsureFinancialAccountOwnerOutboundAllowedAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        string operation,
        CancellationToken ct = default)
    {
        switch (ownerType)
        {
            case FinancialAccountOwnerType.User:
                await EnsureUserOutboundAllowedAsync(
                    ownerId,
                    operation,
                    ct);
                return;

            case FinancialAccountOwnerType.Business:
                await EnsureBusinessOutboundAllowedAsync(
                    ownerId,
                    operation,
                    ct);
                return;

            case FinancialAccountOwnerType.BusinessCustomer:
                await EnsureBusinessCustomerOutboundAllowedAsync(
                    ownerId,
                    operation,
                    ct);
                return;

            case FinancialAccountOwnerType.Platform:
            case FinancialAccountOwnerType.Treasury:
            case FinancialAccountOwnerType.Provider:
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported financial-account owner type '{ownerType}'.");
        }
    }

    public async Task<IReadOnlyList<OutboundFundsRestrictionSubjectLookupDto>> SearchSubjectsAsync(
        OutboundFundsRestrictionSubjectType subjectType,
        string search,
        int take = 20,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(
                typeof(OutboundFundsRestrictionSubjectType),
                subjectType))
        {
            throw new InvalidOperationException(
                "A valid restriction subject type is required.");
        }

        var value = Required(
            search,
            "Search",
            200);

        if (value.Length < 2)
        {
            throw new InvalidOperationException(
                "Search must contain at least 2 characters.");
        }

        take = Math.Clamp(take, 1, 50);

        return subjectType switch
        {
            OutboundFundsRestrictionSubjectType.User =>
                await SearchUsersAsync(value, take, ct),

            OutboundFundsRestrictionSubjectType.BusinessProfile =>
                await SearchBusinessProfilesAsync(value, take, ct),

            OutboundFundsRestrictionSubjectType.BusinessCustomer =>
                await SearchBusinessCustomersAsync(value, take, ct),

            _ => throw new InvalidOperationException(
                "Unsupported restriction subject type.")
        };
    }

    public async Task<PagedResult<OutboundFundsRestrictionDto>> GetAsync(
        OutboundFundsRestrictionSubjectType? subjectType,
        OutboundFundsRestrictionSource? source,
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.OutboundFundsRestrictions
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (subjectType.HasValue)
            query = query.Where(x => x.SubjectType == subjectType.Value);

        if (source.HasValue)
            query = query.Where(x => x.Source == source.Value);

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();

            query = query.Where(x =>
                x.SubjectDisplayName.Contains(value) ||
                x.InternalReason.Contains(value) ||
                (x.ExternalReference != null &&
                 x.ExternalReference.Contains(value)));
        }

        var result = await query
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.AppliedAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<OutboundFundsRestrictionDto>
        {
            Items = result.Items
                .Select(ToDto)
                .ToList(),
            Meta = result.Meta
        };
    }

    public async Task<OutboundFundsRestrictionDto> GetByIdAsync(
        Guid restrictionId,
        CancellationToken ct = default)
    {
        var restriction = await _db.OutboundFundsRestrictions
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.Id == restrictionId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Outbound funds restriction not found.");

        return ToDto(restriction);
    }

    public async Task<OutboundFundsRestrictionDto> ApplyAsync(
        ApplyOutboundFundsRestrictionRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(
                typeof(OutboundFundsRestrictionSubjectType),
                request.SubjectType))
        {
            throw new InvalidOperationException(
                "A valid restriction subject type is required.");
        }

        if (!Enum.IsDefined(
                typeof(OutboundFundsRestrictionSource),
                request.Source))
        {
            throw new InvalidOperationException(
                "A valid restriction source is required.");
        }

        if (request.SubjectId == Guid.Empty)
            throw new InvalidOperationException(
                "A restriction subject is required.");

        var reason = Required(
            request.InternalReason,
            "InternalReason",
            2000);

        var externalReference = Optional(
            request.ExternalReference,
            300);

        var displayName = await ResolveSubjectDisplayNameAsync(
            request.SubjectType,
            request.SubjectId,
            ct);

        var existing = await FindActiveAsync(
            request.SubjectType,
            request.SubjectId,
            ct);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                "An active outbound funds restriction already exists for this subject.");
        }

        var now = DateTime.UtcNow;

        var restriction = new OutboundFundsRestriction
        {
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            SubjectDisplayName = displayName,
            Source = request.Source,
            InternalReason = reason,
            ExternalReference = externalReference,
            IsActive = true,
            AppliedByUserId = actorUserId,
            AppliedAt = now,
            CreatedByUserId = actorUserId,
            CreatedAt = now
        };

        _db.OutboundFundsRestrictions.Add(restriction);

        _audit.Stage(new AuditRecordRequest(
            Action: "OUTBOUND_FUNDS_RESTRICTION_APPLIED",
            Category: "ComplianceRestriction",
            EntityName: nameof(OutboundFundsRestriction),
            EntityId: restriction.Id.ToString(),
            NewValues: new
            {
                restriction.SubjectType,
                restriction.SubjectId,
                restriction.SubjectDisplayName,
                restriction.Source,
                restriction.ExternalReference,
                restriction.IsActive,
                restriction.AppliedAt
            },
            Metadata: new
            {
                InternalReason = reason,
                ActorUserId = actorUserId
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        return ToDto(restriction);
    }

    public async Task<OutboundFundsRestrictionDto> LiftAsync(
        Guid restrictionId,
        LiftOutboundFundsRestrictionRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var reason = Required(
            request.Reason,
            "Reason",
            2000);

        var restriction = await _db.OutboundFundsRestrictions
            .SingleOrDefaultAsync(x =>
                x.Id == restrictionId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Outbound funds restriction not found.");

        if (!restriction.IsActive)
        {
            throw new InvalidOperationException(
                "The outbound funds restriction is already inactive.");
        }

        if (restriction.AppliedByUserId == actorUserId)
        {
            throw new InvalidOperationException(
                "A different authorized reviewer must release an outbound funds restriction.");
        }

        var now = DateTime.UtcNow;

        restriction.IsActive = false;
        restriction.LiftedByUserId = actorUserId;
        restriction.LiftedAt = now;
        restriction.LiftReason = reason;
        restriction.LastUpdatedByUserId = actorUserId;
        restriction.LastUpdatedAt = now;

        _audit.Stage(new AuditRecordRequest(
            Action: "OUTBOUND_FUNDS_RESTRICTION_LIFTED",
            Category: "ComplianceRestriction",
            EntityName: nameof(OutboundFundsRestriction),
            EntityId: restriction.Id.ToString(),
            OldValues: new
            {
                IsActive = true
            },
            NewValues: new
            {
                restriction.IsActive,
                restriction.LiftedByUserId,
                restriction.LiftedAt
            },
            Metadata: new
            {
                LiftReason = reason,
                AppliedByUserId = restriction.AppliedByUserId,
                ActorUserId = actorUserId
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        return ToDto(restriction);
    }

    private async Task<IReadOnlyList<OutboundFundsRestrictionSubjectLookupDto>> SearchUsersAsync(
        string search,
        int take,
        CancellationToken ct)
    {
        var rows = await _db.Users
            .AsNoTracking()
            .Where(x =>
                (x.FirstName != null && x.FirstName.Contains(search)) ||
                (x.LastName != null && x.LastName.Contains(search)) ||
                (x.Email != null && x.Email.Contains(search)) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(search)))
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName,
                x.Email,
                x.CountryCode
            })
            .ToListAsync(ct);

        var ids = rows.Select(x => x.Id).ToList();

        var restrictedIds = await _db.OutboundFundsRestrictions
            .AsNoTracking()
            .Where(x =>
                x.SubjectType == OutboundFundsRestrictionSubjectType.User &&
                ids.Contains(x.SubjectId) &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => x.SubjectId)
            .ToListAsync(ct);

        var restricted = restrictedIds.ToHashSet();

        return rows
            .Select(x =>
            {
                var name = $"{x.FirstName} {x.LastName}".Trim();

                return new OutboundFundsRestrictionSubjectLookupDto(
                    OutboundFundsRestrictionSubjectType.User,
                    x.Id,
                    string.IsNullOrWhiteSpace(name)
                        ? x.Email ?? x.Id.ToString()
                        : name,
                    x.Email,
                    x.CountryCode,
                    restricted.Contains(x.Id));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<OutboundFundsRestrictionSubjectLookupDto>> SearchBusinessProfilesAsync(
        string search,
        int take,
        CancellationToken ct)
    {
        var rows = await _db.BusinessProfiles
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.BusinessName.Contains(search) ||
                 (x.ContactEmail != null &&
                  x.ContactEmail.Contains(search)) ||
                 x.CountryCode.Contains(search)))
            .OrderBy(x => x.BusinessName)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.BusinessName,
                x.ContactEmail,
                x.CountryCode
            })
            .ToListAsync(ct);

        var ids = rows.Select(x => x.Id).ToList();

        var restrictedIds = await _db.OutboundFundsRestrictions
            .AsNoTracking()
            .Where(x =>
                x.SubjectType ==
                    OutboundFundsRestrictionSubjectType.BusinessProfile &&
                ids.Contains(x.SubjectId) &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => x.SubjectId)
            .ToListAsync(ct);

        var restricted = restrictedIds.ToHashSet();

        return rows
            .Select(x =>
                new OutboundFundsRestrictionSubjectLookupDto(
                    OutboundFundsRestrictionSubjectType.BusinessProfile,
                    x.Id,
                    x.BusinessName,
                    x.ContactEmail,
                    x.CountryCode,
                    restricted.Contains(x.Id)))
            .ToList();
    }

    private async Task<IReadOnlyList<OutboundFundsRestrictionSubjectLookupDto>> SearchBusinessCustomersAsync(
        string search,
        int take,
        CancellationToken ct)
    {
        var rows = await _db.BusinessCustomers
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.DisplayName.Contains(search) ||
                 x.ExternalReference.Contains(search) ||
                 (x.Email != null && x.Email.Contains(search)) ||
                 (x.PhoneNumber != null &&
                  x.PhoneNumber.Contains(search)) ||
                 x.BusinessProfile.BusinessName.Contains(search)))
            .OrderBy(x => x.BusinessProfile.BusinessName)
            .ThenBy(x => x.DisplayName)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.ExternalReference,
                x.CountryCode,
                BusinessName = x.BusinessProfile.BusinessName
            })
            .ToListAsync(ct);

        var ids = rows.Select(x => x.Id).ToList();

        var restrictedIds = await _db.OutboundFundsRestrictions
            .AsNoTracking()
            .Where(x =>
                x.SubjectType ==
                    OutboundFundsRestrictionSubjectType.BusinessCustomer &&
                ids.Contains(x.SubjectId) &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => x.SubjectId)
            .ToListAsync(ct);

        var restricted = restrictedIds.ToHashSet();

        return rows
            .Select(x =>
                new OutboundFundsRestrictionSubjectLookupDto(
                    OutboundFundsRestrictionSubjectType.BusinessCustomer,
                    x.Id,
                    $"{x.BusinessName} / {x.DisplayName}",
                    x.ExternalReference,
                    x.CountryCode,
                    restricted.Contains(x.Id)))
            .ToList();
    }

    private async Task<OutboundFundsRestriction?> FindActiveAsync(
        OutboundFundsRestrictionSubjectType subjectType,
        Guid subjectId,
        CancellationToken ct) =>
        await _db.OutboundFundsRestrictions
            .AsNoTracking()
            .Where(x =>
                x.SubjectType == subjectType &&
                x.SubjectId == subjectId &&
                x.IsActive &&
                !x.IsDeleted)
            .OrderByDescending(x => x.AppliedAt)
            .FirstOrDefaultAsync(ct);

    private void ThrowIfRestricted(
        OutboundFundsRestriction? restriction,
        string operation)
    {
        if (restriction is null)
            return;

        _logger.LogWarning(
            "Outbound funds restriction blocked operation {Operation}. RestrictionId: {RestrictionId}; SubjectType: {SubjectType}; SubjectId: {SubjectId}",
            string.IsNullOrWhiteSpace(operation)
                ? "outbound_money_movement"
                : operation.Trim(),
            restriction.Id,
            restriction.SubjectType,
            restriction.SubjectId);

        throw new OutboundFundsRestrictedException();
    }

    private async Task<string> ResolveSubjectDisplayNameAsync(
        OutboundFundsRestrictionSubjectType subjectType,
        Guid subjectId,
        CancellationToken ct)
    {
        switch (subjectType)
        {
            case OutboundFundsRestrictionSubjectType.User:
            {
                var user = await _db.Users
                    .AsNoTracking()
                    .Where(x => x.Id == subjectId)
                    .Select(x => new
                    {
                        x.FirstName,
                        x.LastName,
                        x.Email
                    })
                    .SingleOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException(
                        "User not found.");

                var name = $"{user.FirstName} {user.LastName}".Trim();
                return string.IsNullOrWhiteSpace(name)
                    ? user.Email ?? subjectId.ToString()
                    : name;
            }

            case OutboundFundsRestrictionSubjectType.BusinessProfile:
            {
                return await _db.BusinessProfiles
                    .AsNoTracking()
                    .Where(x => x.Id == subjectId && !x.IsDeleted)
                    .Select(x => x.BusinessName)
                    .SingleOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException(
                        "Business profile not found.");
            }

            case OutboundFundsRestrictionSubjectType.BusinessCustomer:
            {
                var customer = await _db.BusinessCustomers
                    .AsNoTracking()
                    .Where(x => x.Id == subjectId && !x.IsDeleted)
                    .Select(x => new
                    {
                        x.DisplayName,
                        BusinessName = x.BusinessProfile.BusinessName
                    })
                    .SingleOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException(
                        "Business customer not found.");

                return $"{customer.BusinessName} / {customer.DisplayName}";
            }

            default:
                throw new InvalidOperationException(
                    "Unsupported restriction subject type.");
        }
    }

    private static OutboundFundsRestrictionDto ToDto(
        OutboundFundsRestriction restriction) =>
        new(
            restriction.Id,
            restriction.SubjectType,
            restriction.SubjectId,
            restriction.SubjectDisplayName,
            restriction.Source,
            restriction.InternalReason,
            restriction.ExternalReference,
            restriction.IsActive,
            restriction.AppliedByUserId,
            restriction.AppliedAt,
            restriction.LiftedByUserId,
            restriction.LiftedAt,
            restriction.LiftReason,
            restriction.CreatedAt,
            restriction.LastUpdatedAt);

    private static string Required(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{fieldName} is required.");

        var cleaned = value.Trim();

        if (cleaned.Length > maxLength)
        {
            throw new InvalidOperationException(
                $"{fieldName} cannot exceed {maxLength} characters.");
        }

        return cleaned;
    }

    private static string? Optional(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim();

        if (cleaned.Length > maxLength)
        {
            throw new InvalidOperationException(
                $"Value cannot exceed {maxLength} characters.");
        }

        return cleaned;
    }
}
