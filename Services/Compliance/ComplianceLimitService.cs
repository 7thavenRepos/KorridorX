using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Compliance;
using KorridorX.Extensions;
using KorridorX.Exceptions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public sealed class ComplianceLimitService : IComplianceLimitService
{
    private static readonly TransferStatus[] CountedStatuses =
    [
        TransferStatus.PendingApproval,
        TransferStatus.PendingPayment,
        TransferStatus.PaymentReceived,
        TransferStatus.Processing,
        TransferStatus.PayoutInitiated,
        TransferStatus.PayoutCompleted,
        TransferStatus.Completed,
        TransferStatus.RefundPending
    ];

    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ComplianceLimitService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ComplianceLimitDecision> EvaluateAsync(
        Transfer transfer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        var customerType = transfer.BusinessProfileId.HasValue
            ? CustomerType.Business
            : CustomerType.Individual;

        var limit = await _db.ComplianceLimits
            .AsNoTracking()
            .Where(x =>
                x.CustomerType == customerType &&
                x.CountryCode == transfer.SourceCountryCode &&
                x.CurrencyCode == transfer.SourceCurrencyCode &&
                x.IsActive &&
                !x.IsDeleted)
            .OrderByDescending(x => x.LastUpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (limit is null)
        {
            return new ComplianceLimitDecision(
                true,
                null,
                null,
                0,
                0,
                0,
                0,
                0);
        }

        var now = DateTime.UtcNow;
        var startOfDay = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var query = _db.Transfers
            .AsNoTracking()
            .Where(x =>
                x.Id != transfer.Id &&
                !x.IsDeleted &&
                x.SourceCountryCode == transfer.SourceCountryCode &&
                x.SourceCurrencyCode == transfer.SourceCurrencyCode &&
                CountedStatuses.Contains(x.Status));

        query = transfer.BusinessProfileId.HasValue
            ? query.Where(x => x.BusinessProfileId == transfer.BusinessProfileId)
            : query.Where(x => x.CustomerProfileId == transfer.CustomerProfileId);

        var dailyUsed = await query
            .Where(x => x.CreatedAt >= startOfDay)
            .SumAsync(x => (decimal?)x.TotalPayableAmount, ct) ?? 0m;

        var monthlyUsed = await query
            .Where(x => x.CreatedAt >= startOfMonth)
            .SumAsync(x => (decimal?)x.TotalPayableAmount, ct) ?? 0m;

        var trackedTransfers = _db.ChangeTracker.Entries<Transfer>()
            .Where(x =>
                x.State == EntityState.Added &&
                x.Entity.Id != transfer.Id &&
                !x.Entity.IsDeleted &&
                x.Entity.SourceCountryCode == transfer.SourceCountryCode &&
                x.Entity.SourceCurrencyCode == transfer.SourceCurrencyCode &&
                CountedStatuses.Contains(x.Entity.Status) &&
                (transfer.BusinessProfileId.HasValue
                    ? x.Entity.BusinessProfileId == transfer.BusinessProfileId
                    : x.Entity.CustomerProfileId == transfer.CustomerProfileId))
            .Select(x => x.Entity)
            .ToList();

        dailyUsed += trackedTransfers
            .Where(x => x.CreatedAt >= startOfDay)
            .Sum(x => x.TotalPayableAmount);
        monthlyUsed += trackedTransfers
            .Where(x => x.CreatedAt >= startOfMonth)
            .Sum(x => x.TotalPayableAmount);

        return ComplianceLimitPolicy.Evaluate(
            transfer.TotalPayableAmount,
            dailyUsed,
            monthlyUsed,
            limit);
    }

    public async Task EnsureWithinLimitsAsync(
        Transfer transfer,
        CancellationToken ct = default)
    {
        var decision = await EvaluateAsync(transfer, ct);

        if (!decision.IsAllowed)
            throw new ComplianceLimitExceededException(
                decision.FailureCode ?? "COMPLIANCE_LIMIT_EXCEEDED",
                decision.FailureMessage ?? "The transfer exceeds a compliance limit.");

        _db.ComplianceChecks.Add(new ComplianceCheck
        {
            CustomerProfileId = transfer.CustomerProfileId,
            TransferId = transfer.Id,
            CheckType = "COMPLIANCE_LIMIT",
            Status = "PASSED",
            ResultJson = System.Text.Json.JsonSerializer.Serialize(decision),
            CheckedAt = DateTime.UtcNow
        });
    }

    public Task<PagedResult<ComplianceLimitDto>> GetLimitsAsync(
        string? countryCode,
        string? currencyCode,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ComplianceLimits.AsNoTracking().Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var normalized = countryCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.CountryCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var normalized = currencyCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.CurrencyCode == normalized);
        }

        return query
            .OrderBy(x => x.CustomerType)
            .ThenBy(x => x.CountryCode)
            .ThenBy(x => x.CurrencyCode)
            .Select(x => new ComplianceLimitDto(
                x.Id,
                x.CustomerType,
                x.CountryCode,
                x.CurrencyCode,
                x.DailyLimit,
                x.MonthlyLimit,
                x.PerTransferLimit,
                x.IsActive,
                x.CreatedAt,
                x.LastUpdatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<ComplianceLimitDto> UpsertAsync(
        Guid userId,
        UpsertComplianceLimitRequestDto request,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(CustomerType), request.CustomerType))
            throw new InvalidOperationException("A valid customer type is required.");
        if (request.DailyLimit < request.PerTransferLimit)
            throw new InvalidOperationException("The daily limit cannot be lower than the per-transfer limit.");
        if (request.MonthlyLimit < request.DailyLimit)
            throw new InvalidOperationException("The monthly limit cannot be lower than the daily limit.");

        var countryCode = request.CountryCode.Trim().ToUpperInvariant();
        var currencyCode = request.CurrencyCode.Trim().ToUpperInvariant();

        var limit = await _db.ComplianceLimits.FirstOrDefaultAsync(x =>
            x.CustomerType == request.CustomerType &&
            x.CountryCode == countryCode &&
            x.CurrencyCode == currencyCode &&
            !x.IsDeleted,
            ct);

        var oldValues = limit is null
            ? null
            : new
            {
                limit.DailyLimit,
                limit.MonthlyLimit,
                limit.PerTransferLimit,
                limit.IsActive
            };

        if (limit is null)
        {
            limit = new ComplianceLimit
            {
                CustomerType = request.CustomerType,
                CountryCode = countryCode,
                CurrencyCode = currencyCode,
                CreatedByUserId = userId
            };
            _db.ComplianceLimits.Add(limit);
        }

        limit.DailyLimit = request.DailyLimit;
        limit.MonthlyLimit = request.MonthlyLimit;
        limit.PerTransferLimit = request.PerTransferLimit;
        limit.IsActive = request.IsActive;
        limit.LastUpdatedAt = DateTime.UtcNow;
        limit.LastUpdatedByUserId = userId;

        _audit.Stage(new AuditRecordRequest(
            "COMPLIANCE_LIMIT_UPSERTED",
            "Compliance",
            nameof(ComplianceLimit),
            limit.Id.ToString(),
            oldValues,
            new
            {
                limit.CustomerType,
                limit.CountryCode,
                limit.CurrencyCode,
                limit.DailyLimit,
                limit.MonthlyLimit,
                limit.PerTransferLimit,
                limit.IsActive
            },
            null,
            userId));

        await _db.SaveChangesAsync(ct);

        return new ComplianceLimitDto(
            limit.Id,
            limit.CustomerType,
            limit.CountryCode,
            limit.CurrencyCode,
            limit.DailyLimit,
            limit.MonthlyLimit,
            limit.PerTransferLimit,
            limit.IsActive,
            limit.CreatedAt,
            limit.LastUpdatedAt);
    }
}
