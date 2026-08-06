using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public interface IComplianceGateService
{
    Task<ComplianceGateResult> EnsureCanInitiateMoneyMovementAsync(
        Guid customerProfileId,
        ProviderCode providerCode,
        CancellationToken ct = default);

    Task<ComplianceGateResult> EnsureBusinessCanInitiateMoneyMovementAsync(
        Guid businessProfileId,
        ProviderCode providerCode,
        CancellationToken ct = default);
}

public record ComplianceGateResult(
    string ProviderCustomerId,
    string ProviderStatus);
