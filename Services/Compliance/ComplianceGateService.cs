using KorridorX.Data;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public class ComplianceGateService : IComplianceGateService
{
    private readonly AppDbContext _db;

    public ComplianceGateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ComplianceGateResult> EnsureCanInitiateMoneyMovementAsync(
        Guid customerProfileId,
        ProviderCode providerCode,
        CancellationToken ct = default)
    {
        var customer = await _db.CustomerProfiles
            .AsNoTracking()
            .Where(x => x.Id == customerProfileId && !x.IsDeleted)
            .Select(x => new { x.KycStatus })
            .FirstOrDefaultAsync(ct);

        if (customer is null)
        {
            throw new InvalidOperationException("Customer profile not found.");
        }

        if (customer.KycStatus != KycStatus.Approved)
        {
            throw new InvalidOperationException(
                "KYC approval is required before initiating money movement.");
        }

        var kycApproved = await _db.KycProfiles
            .AsNoTracking()
            .AnyAsync(x =>
                x.CustomerProfileId == customerProfileId &&
                x.Status == KycStatus.Approved &&
                !x.IsDeleted,
                ct);

        if (!kycApproved)
        {
            throw new InvalidOperationException(
                "An approved compliance profile is required before initiating money movement.");
        }

        var providerCustomer = await _db.ProviderCustomers
            .AsNoTracking()
            .Where(x =>
                x.CustomerProfileId == customerProfileId &&
                x.ProviderCode == providerCode &&
                !x.IsDeleted)
            .Select(x => new
            {
                x.ProviderCustomerId,
                x.ProviderStatus
            })
            .FirstOrDefaultAsync(ct);

        if (providerCustomer is null)
        {
            throw new InvalidOperationException(
                "A provider customer record is required before initiating money movement.");
        }

        if (!string.Equals(providerCustomer.ProviderStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The provider customer must be VERIFIED before initiating money movement.");
        }

        return new ComplianceGateResult(
            providerCustomer.ProviderCustomerId,
            providerCustomer.ProviderStatus ?? "VERIFIED");
    }

    public async Task<ComplianceGateResult> EnsureBusinessCanInitiateMoneyMovementAsync(
        Guid businessProfileId,
        ProviderCode providerCode,
        CancellationToken ct = default)
    {
        var business = await _db.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.Id == businessProfileId && !x.IsDeleted)
            .Select(x => new { x.KybStatus })
            .FirstOrDefaultAsync(ct);

        if (business is null)
        {
            throw new InvalidOperationException("Business profile not found.");
        }

        if (business.KybStatus != KybStatus.Approved)
        {
            throw new InvalidOperationException(
                "KYB approval is required before the business can initiate money movement.");
        }

        var applicationApproved = await _db.BusinessKybApplications
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessProfileId == businessProfileId &&
                x.Status == KybStatus.Approved &&
                !x.IsDeleted,
                ct);

        if (!applicationApproved)
        {
            throw new InvalidOperationException(
                "An approved business compliance application is required before initiating money movement.");
        }

        var providerCustomer = await _db.ProviderCustomers
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == businessProfileId &&
                x.ProviderCode == providerCode &&
                !x.IsDeleted)
            .Select(x => new
            {
                x.ProviderCustomerId,
                x.ProviderStatus
            })
            .FirstOrDefaultAsync(ct);

        if (providerCustomer is null)
        {
            throw new InvalidOperationException(
                "A provider business customer record is required before initiating money movement.");
        }

        if (!string.Equals(providerCustomer.ProviderStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The provider business customer must be VERIFIED before initiating money movement.");
        }

        return new ComplianceGateResult(
            providerCustomer.ProviderCustomerId,
            providerCustomer.ProviderStatus ?? "VERIFIED");
    }
}
