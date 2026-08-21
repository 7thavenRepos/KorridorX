using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class CollectionAccountProvisioningService : ICollectionAccountProvisioningService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddedFinanceContextAccessor _context;
    private readonly IReadOnlyDictionary<string, ICollectionAccountProvisioner> _provisioners;

    public CollectionAccountProvisioningService(
        AppDbContext db,
        IEmbeddedFinanceContextAccessor context,
        IEnumerable<ICollectionAccountProvisioner> provisioners)
    {
        _db = db;
        _context = context;
        _provisioners = provisioners.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ProviderAccountMappingDto> ProvisionAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        ProvisionCollectionAccountRequestDto request,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.AccountsWrite);
        var providerCode = Required(request.ProviderCode, 50, "Provider code");

        var account = await _db.CollectionAccounts
            .Include(x => x.BusinessCustomer)
            .FirstOrDefaultAsync(x =>
                x.Id == collectionAccountId &&
                x.BusinessCustomerId == businessCustomerId &&
                x.BusinessProfileId == principal.BusinessProfileId &&
                !x.IsDeleted &&
                !x.BusinessCustomer.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Collection account not found.");

        if (account.BusinessCustomer.Status != BusinessCustomerStatus.Active)
            throw new InvalidOperationException("Business customer must be active.");

        var existing = await _db.ProviderAccountMappings.FirstOrDefaultAsync(x =>
            x.CollectionAccountId == account.Id &&
            x.ProviderCode == providerCode &&
            !x.IsDeleted,
            ct);

        if (existing?.Status == ProviderAccountMappingStatus.Active)
            return ToDto(existing);

        if (!_provisioners.TryGetValue(providerCode, out var provisioner))
            throw new InvalidOperationException(
                $"Provider '{providerCode}' does not expose collection-account provisioning in this deployment.");

        if (!provisioner.Supports(account.BusinessCustomer.CountryCode, account.AssetCode))
            throw new InvalidOperationException(
                $"Provider '{providerCode}' does not support collection-account provisioning for {account.BusinessCustomer.CountryCode}/{account.AssetCode}.");

        var mapping = existing ?? new ProviderAccountMapping
        {
            CollectionAccountId = account.Id,
            ProviderCode = providerCode,
            Status = ProviderAccountMappingStatus.Pending
        };

        if (existing is null)
            _db.ProviderAccountMappings.Add(mapping);

        mapping.Status = ProviderAccountMappingStatus.Pending;
        mapping.FailureReason = null;
        mapping.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            var result = await provisioner.ProvisionAsync(
                new CollectionAccountProvisioningRequest(
                    principal.BusinessProfileId,
                    account.BusinessCustomerId,
                    account.Id,
                    account.ExternalReference,
                    account.BusinessCustomer.DisplayName,
                    account.BusinessCustomer.Email,
                    account.BusinessCustomer.PhoneNumber,
                    account.BusinessCustomer.CountryCode,
                    account.AssetCode),
                ct);

            if (string.IsNullOrWhiteSpace(result.ProviderAccountId))
                throw new InvalidOperationException("Provider did not return a provider account identifier.");

            mapping.ProviderCustomerId = Clean(result.ProviderCustomerId, 150);
            mapping.ProviderAccountId = Required(result.ProviderAccountId, 150, "Provider account ID");
            mapping.ProviderReference = Clean(result.ProviderReference, 150);
            mapping.AccountNumber = Clean(result.AccountNumber, 150);
            mapping.AccountName = Clean(result.AccountName, 200);
            mapping.BankName = Clean(result.BankName, 200);
            mapping.MetadataJson = result.MetadataJson;
            mapping.Status = ProviderAccountMappingStatus.Active;
            mapping.FailureReason = null;
            mapping.LastUpdatedAt = DateTime.UtcNow;

            account.Status = CollectionAccountStatus.Active;
            account.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDto(mapping);
        }
        catch (Exception ex)
        {
            mapping.Status = ProviderAccountMappingStatus.Failed;
            mapping.FailureReason = Truncate(ex.Message, 1000);
            mapping.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<ProviderAccountMappingDto>> GetMappingsAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.AccountsRead);
        var exists = await _db.CollectionAccounts.AsNoTracking().AnyAsync(x =>
            x.Id == collectionAccountId &&
            x.BusinessCustomerId == businessCustomerId &&
            x.BusinessProfileId == principal.BusinessProfileId &&
            !x.IsDeleted,
            ct);

        if (!exists)
            throw new InvalidOperationException("Collection account not found.");

        return await _db.ProviderAccountMappings.AsNoTracking()
            .Where(x => x.CollectionAccountId == collectionAccountId && !x.IsDeleted)
            .OrderBy(x => x.ProviderCode)
            .Select(x => new ProviderAccountMappingDto(
                x.Id, x.CollectionAccountId, x.ProviderCode, x.ProviderCustomerId,
                x.ProviderAccountId, x.ProviderReference, x.AccountNumber,
                x.AccountName, x.BankName, x.Status, x.FailureReason,
                x.CreatedAt, x.LastUpdatedAt))
            .ToListAsync(ct);
    }

    private EmbeddedFinancePrincipal RequireScope(EmbeddedFinanceScope scope)
    {
        var principal = _context.GetRequiredPrincipal();
        if (!principal.HasScope(scope))
            throw new UnauthorizedAccessException($"API application does not have required scope '{scope}'.");
        return principal;
    }

    private static ProviderAccountMappingDto ToDto(ProviderAccountMapping x) => new(
        x.Id, x.CollectionAccountId, x.ProviderCode, x.ProviderCustomerId,
        x.ProviderAccountId, x.ProviderReference, x.AccountNumber,
        x.AccountName, x.BankName, x.Status, x.FailureReason, x.CreatedAt, x.LastUpdatedAt);

    private static string Required(string? value, int max, string label)
    {
        var cleaned = (value ?? "").Trim();
        if (cleaned.Length == 0) throw new InvalidOperationException($"{label} is required.");
        if (cleaned.Length > max) throw new InvalidOperationException($"{label} cannot exceed {max} characters.");
        return cleaned;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= max ? cleaned : cleaned[..max];
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
