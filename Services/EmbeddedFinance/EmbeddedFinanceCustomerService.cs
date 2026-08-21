using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinanceCustomerService : IEmbeddedFinanceCustomerService
{
    private readonly AppDbContext _db; private readonly IEmbeddedFinanceContextAccessor _context;
    public EmbeddedFinanceCustomerService(AppDbContext db, IEmbeddedFinanceContextAccessor context) { _db = db; _context = context; }

    public async Task<PagedResult<BusinessCustomerDto>> GetCustomersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var p = RequireScope(EmbeddedFinanceScope.CustomersRead);
        return await _db.BusinessCustomers.AsNoTracking().Where(x => x.BusinessProfileId == p.BusinessProfileId && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).Select(x => ToCustomerDto(x)).PaginateAsync(page, pageSize, ct);
    }

    public async Task<BusinessCustomerDto> CreateCustomerAsync(CreateBusinessCustomerRequestDto request, string idempotencyKey, CancellationToken ct = default)
    {
        var p = RequireScope(EmbeddedFinanceScope.CustomersWrite); var key = NormalizeKey(idempotencyKey);
        var normalized = new { ExternalReference = Required(request.ExternalReference,150,"External reference"), DisplayName = Required(request.DisplayName,200,"Display name"), Email = Optional(request.Email,255), PhoneNumber = Optional(request.PhoneNumber,50), CountryCode = Required(request.CountryCode,10,"Country code").ToUpperInvariant(), request.MetadataJson };
        if (!await _db.Countries.AsNoTracking().AnyAsync(
                x => x.Code == normalized.CountryCode && x.IsSupported,
                ct))
            throw new InvalidOperationException("Business customer country is not supported.");

        var hash = Hash(normalized); var idem = await FindIdem(p.ApiApplicationId,key,ct);
        if (idem is not null) { Same(idem,hash); return await _db.BusinessCustomers.AsNoTracking().Where(x => x.Id == idem.ResourceId && x.BusinessProfileId == p.BusinessProfileId).Select(x => ToCustomerDto(x)).SingleAsync(ct); }
        if (await _db.BusinessCustomers.AnyAsync(x => x.BusinessProfileId == p.BusinessProfileId && x.ExternalReference == normalized.ExternalReference && !x.IsDeleted,ct)) throw new InvalidOperationException("A business customer with this external reference already exists.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var customer = new BusinessCustomer { BusinessProfileId=p.BusinessProfileId, ExternalReference=normalized.ExternalReference, DisplayName=normalized.DisplayName, Email=normalized.Email, PhoneNumber=normalized.PhoneNumber, CountryCode=normalized.CountryCode, MetadataJson=normalized.MetadataJson, Status=BusinessCustomerStatus.Active };
        _db.BusinessCustomers.Add(customer); _db.EmbeddedApiIdempotencyRecords.Add(new EmbeddedApiIdempotencyRecord { ApiApplicationId=p.ApiApplicationId, IdempotencyKey=key, RequestHash=hash, ResourceType=nameof(BusinessCustomer), ResourceId=customer.Id });
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToCustomerDto(customer);
    }

    public async Task<IReadOnlyList<CollectionAccountDto>> GetAccountsAsync(Guid businessCustomerId, CancellationToken ct = default)
    {
        var p = RequireScope(EmbeddedFinanceScope.AccountsRead); await EnsureCustomer(p.BusinessProfileId,businessCustomerId,ct);
        return await _db.CollectionAccounts.AsNoTracking().Where(x => x.BusinessProfileId == p.BusinessProfileId && x.BusinessCustomerId == businessCustomerId && !x.IsDeleted).OrderBy(x => x.AssetCode).Select(x => ToAccountDto(x)).ToListAsync(ct);
    }

    public async Task<CollectionAccountDto> CreateAccountAsync(Guid businessCustomerId, CreateCollectionAccountRequestDto request, string idempotencyKey, CancellationToken ct = default)
    {
        var p = RequireScope(EmbeddedFinanceScope.AccountsWrite); var key = NormalizeKey(idempotencyKey); var customer = await EnsureCustomer(p.BusinessProfileId,businessCustomerId,ct);
        if (customer.Status != BusinessCustomerStatus.Active) throw new InvalidOperationException("Business customer must be active.");
        var normalized = new { BusinessCustomerId=businessCustomerId, ExternalReference=Required(request.ExternalReference,150,"External reference"), AssetCode=Required(request.AssetCode,20,"Asset code").ToUpperInvariant() };
        var hash=Hash(normalized); var idem=await FindIdem(p.ApiApplicationId,key,ct);
        if (idem is not null) { Same(idem,hash); return await _db.CollectionAccounts.AsNoTracking().Where(x => x.Id == idem.ResourceId && x.BusinessProfileId == p.BusinessProfileId).Select(x => ToAccountDto(x)).SingleAsync(ct); }
        var asset = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Code == normalized.AssetCode && x.IsSupported,ct) ?? throw new InvalidOperationException($"Asset '{normalized.AssetCode}' is not supported.");
        if (await _db.CollectionAccounts.AnyAsync(x => x.BusinessCustomerId == businessCustomerId && x.AssetCode == asset.Code && !x.IsDeleted,ct)) throw new InvalidOperationException($"The business customer already has a {asset.Code} collection account.");
        if (await _db.CollectionAccounts.AnyAsync(x => x.BusinessProfileId == p.BusinessProfileId && x.ExternalReference == normalized.ExternalReference && !x.IsDeleted,ct)) throw new InvalidOperationException("A collection account with this external reference already exists.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var fa = new FinancialAccount { OwnerType=FinancialAccountOwnerType.BusinessCustomer, OwnerId=customer.Id, AccountCode=BuildAccountCode(customer.Id,asset.Code), AssetCode=asset.Code, AccountType=FinancialAccountType.Customer, Status=FinancialAccountStatus.Active, SettledBalance=0m, AvailableBalance=0m, HeldBalance=0m };
        var ca = new CollectionAccount { BusinessProfileId=p.BusinessProfileId, BusinessCustomerId=customer.Id, ExternalReference=normalized.ExternalReference, AssetCode=asset.Code, FinancialAccountId=fa.Id, FinancialAccount=fa, Status=CollectionAccountStatus.Pending };
        _db.FinancialAccounts.Add(fa); _db.CollectionAccounts.Add(ca); _db.EmbeddedApiIdempotencyRecords.Add(new EmbeddedApiIdempotencyRecord { ApiApplicationId=p.ApiApplicationId, IdempotencyKey=key, RequestHash=hash, ResourceType=nameof(CollectionAccount), ResourceId=ca.Id });
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToAccountDto(ca);
    }

    private EmbeddedFinancePrincipal RequireScope(EmbeddedFinanceScope scope) { var p=_context.GetRequiredPrincipal(); if(!p.HasScope(scope)) throw new UnauthorizedAccessException($"API application does not have required scope '{scope}'."); return p; }
    private async Task<BusinessCustomer> EnsureCustomer(Guid businessId,Guid customerId,CancellationToken ct)=>await _db.BusinessCustomers.FirstOrDefaultAsync(x=>x.Id==customerId&&x.BusinessProfileId==businessId&&!x.IsDeleted,ct)??throw new InvalidOperationException("Business customer not found.");
    private async Task<EmbeddedApiIdempotencyRecord?> FindIdem(Guid appId,string key,CancellationToken ct)=>await _db.EmbeddedApiIdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x=>x.ApiApplicationId==appId&&x.IdempotencyKey==key&&!x.IsDeleted,ct);
    private static void Same(EmbeddedApiIdempotencyRecord r,string h){if(!string.Equals(r.RequestHash,h,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The Idempotency-Key has already been used with a different request.");}
    private static string NormalizeKey(string v){var x=(v??"").Trim();if(x.Length==0)throw new InvalidOperationException("Idempotency-Key header is required.");if(x.Length>200)throw new InvalidOperationException("Idempotency-Key cannot exceed 200 characters.");return x;}
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string BuildAccountCode(Guid id,string asset){var x=$"BC-{id:N}-{asset}";return x.Length<=80?x:x[..80];}
    private static BusinessCustomerDto ToCustomerDto(BusinessCustomer x)=>new(x.Id,x.ExternalReference,x.DisplayName,x.Email,x.PhoneNumber,x.CountryCode,x.Status,x.MetadataJson,x.CreatedAt,x.LastUpdatedAt);
    private static CollectionAccountDto ToAccountDto(CollectionAccount x)=>new(x.Id,x.BusinessCustomerId,x.ExternalReference,x.AssetCode,x.FinancialAccountId,x.Status,x.FinancialAccount.SettledBalance,x.FinancialAccount.AvailableBalance,x.FinancialAccount.HeldBalance,x.CreatedAt);
    private static string Required(string v,int max,string label){var x=(v??"").Trim();if(x.Length==0)throw new InvalidOperationException($"{label} is required.");if(x.Length>max)throw new InvalidOperationException($"{label} cannot exceed {max} characters.");return x;}
    private static string? Optional(string? v,int max){if(string.IsNullOrWhiteSpace(v))return null;var x=v.Trim();if(x.Length>max)throw new InvalidOperationException($"Value cannot exceed {max} characters.");return x;}
}
