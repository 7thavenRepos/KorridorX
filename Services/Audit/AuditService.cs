using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Audit;

public sealed partial class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Stage(AuditRecordRequest request)
    {
        _db.AuditLogs.Add(CreateLog(request));
    }

    public async Task RecordAsync(
        AuditRecordRequest request,
        CancellationToken ct = default)
    {
        Stage(request);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogDto>> GetAsync(
        string? category,
        string? action,
        string? entityName,
        Guid? userId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsNoTracking().Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category.Trim());
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(x => x.Action == action.Trim());
        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(x => x.EntityName == entityName.Trim());
        if (userId is not null)
            query = query.Where(x => x.UserId == userId);
        if (from is not null)
            query = query.Where(x => x.OccurredAt >= from.Value.ToUniversalTime());
        if (to is not null)
            query = query.Where(x => x.OccurredAt <= to.Value.ToUniversalTime());

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new AuditLogDto(
                x.Id,
                x.UserId,
                x.Action,
                x.Category,
                x.EntityName,
                x.EntityId,
                x.OldValuesJson,
                x.NewValuesJson,
                x.MetadataJson,
                x.IpAddress,
                x.UserAgent,
                x.CorrelationId,
                x.OccurredAt))
            .PaginateAsync(page, pageSize, ct);
    }

    private AuditLog CreateLog(AuditRecordRequest request)
    {
        var context = _httpContextAccessor.HttpContext;
        var userId = request.UserId ?? ResolveUserId(context);

        return new AuditLog
        {
            UserId = userId,
            Action = TruncateRequired(request.Action, 150),
            Category = TruncateRequired(request.Category, 100),
            EntityName = TruncateRequired(request.EntityName, 150),
            EntityId = TruncateNullable(request.EntityId, 100),
            OldValuesJson = SerializeAndRedact(request.OldValues),
            NewValuesJson = SerializeAndRedact(request.NewValues),
            MetadataJson = SerializeAndRedact(request.Metadata),
            IpAddress = TruncateNullable(context?.Connection.RemoteIpAddress?.ToString(), 100),
            UserAgent = TruncateNullable(context?.Request.Headers["User-Agent"].ToString(), 1000),
            CorrelationId = TruncateNullable(context?.TraceIdentifier, 100),
            OccurredAt = DateTime.UtcNow
        };
    }

    private static Guid? ResolveUserId(HttpContext? context)
    {
        var value = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static string? SerializeAndRedact(object? value)
    {
        if (value is null)
            return null;

        var json = JsonSerializer.Serialize(value);
        return SensitiveJsonPropertyRegex().Replace(
            json,
            match => $"{match.Groups[1].Value}\"[REDACTED]\"");
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Audit action, category, and entity name are required.");
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    [GeneratedRegex("(\\\"(?:password|clientSecret|client_secret|cardNumber|card_number|cvc|cvv|identityNumber|identity_number|accountNumber|account_number|token|authorization)\\\"\\s*:\\s*)\\\"[^\\\"]*\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveJsonPropertyRegex();
}
