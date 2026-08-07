using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public interface IDataRetentionService
{
    Task<PagedResult<RetentionPolicyDto>> GetPoliciesAsync(
        bool? isActive,
        RetentionRecordType? recordType,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<RetentionPolicyDto> CreatePolicyAsync(
        Guid userId,
        UpsertRetentionPolicyRequestDto request,
        CancellationToken ct = default);

    Task<RetentionPolicyDto> UpdatePolicyAsync(
        Guid policyId,
        Guid userId,
        UpsertRetentionPolicyRequestDto request,
        CancellationToken ct = default);

    Task<PagedResult<LegalHoldDto>> GetLegalHoldsAsync(
        LegalHoldStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<LegalHoldDto> CreateLegalHoldAsync(
        Guid userId,
        CreateLegalHoldRequestDto request,
        CancellationToken ct = default);

    Task<LegalHoldDto> ReleaseLegalHoldAsync(
        Guid legalHoldId,
        Guid userId,
        ReleaseLegalHoldRequestDto request,
        CancellationToken ct = default);

    Task<RetentionExecutionDto> ExecutePolicyAsync(
        Guid policyId,
        bool dryRun,
        Guid? userId,
        CancellationToken ct = default);

    Task<int> ExecuteActivePoliciesAsync(
        int batchSize,
        CancellationToken ct = default);
}
