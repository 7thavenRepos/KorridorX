using System.Globalization;
using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinanceAdminAssuranceService
    : IEmbeddedFinanceAdminAssuranceService
{
    private readonly AppDbContext _db;
    private readonly IBusinessPricingService _pricing;

    public EmbeddedFinanceAdminAssuranceService(
        AppDbContext db,
        IBusinessPricingService pricing)
    {
        _db = db;
        _pricing = pricing;
    }

    public async Task<EmbeddedFinanceAdminAssuranceDto> GetAsync(
        Guid? businessProfileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
            throw new InvalidOperationException(
                "Assurance start date cannot be after the end date.");

        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);
        var last30Days = now.AddDays(-30);
        var staleProviderBefore = now.AddMinutes(-30);
        var credentialExpiryBefore = now.AddDays(30);

        EmbeddedFinanceAdminAssuranceBusinessDto? business = null;

        if (businessProfileId.HasValue)
        {
            var row = await _db.BusinessProfiles
                .AsNoTracking()
                .Where(x =>
                    x.Id == businessProfileId.Value &&
                    !x.IsDeleted)
                .Select(x => new
                {
                    x.Id,
                    x.BusinessName,
                    x.CountryCode,
                    x.KybStatus
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException(
                    "Business profile not found.");

            business = new EmbeddedFinanceAdminAssuranceBusinessDto(
                row.Id,
                row.BusinessName,
                row.CountryCode,
                row.KybStatus.ToString());
        }

        var pricing = await BuildPricingAsync(
            businessProfileId,
            fromUtc,
            toUtc,
            ct);

        var security = await BuildSecurityAsync(
            businessProfileId,
            now,
            last24Hours,
            credentialExpiryBefore,
            ct);

        var compliance = await BuildComplianceAsync(
            businessProfileId,
            business?.KybStatus,
            last24Hours,
            last30Days,
            ct);

        var operations = await BuildOperationsAsync(
            businessProfileId,
            last24Hours,
            staleProviderBefore,
            ct);

        return new EmbeddedFinanceAdminAssuranceDto(
            business,
            pricing,
            security,
            compliance,
            operations,
            now);
    }

    public async Task<byte[]> ExportCsvAsync(
        Guid? businessProfileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var assurance = await GetAsync(
            businessProfileId,
            fromUtc,
            toUtc,
            ct);

        var csv = new StringBuilder();
        csv.AppendLine("Section,Subject,Identifier,Metric,Value");

        AddCsvRow(
            csv,
            "Context",
            "Snapshot",
            null,
            "GeneratedAtUtc",
            assurance.GeneratedAt);

        AddCsvRow(
            csv,
            "Context",
            "Snapshot",
            null,
            "FromUtc",
            fromUtc);

        AddCsvRow(
            csv,
            "Context",
            "Snapshot",
            null,
            "ToUtc",
            toUtc);

        if (assurance.Business is not null)
        {
            AddCsvRow(
                csv,
                "Business",
                assurance.Business.BusinessName,
                assurance.Business.BusinessProfileId.ToString(),
                "CountryCode",
                assurance.Business.CountryCode);

            AddCsvRow(
                csv,
                "Business",
                assurance.Business.BusinessName,
                assurance.Business.BusinessProfileId.ToString(),
                "KybStatus",
                assurance.Business.KybStatus);
        }
        else
        {
            AddCsvRow(
                csv,
                "Business",
                "Platform",
                null,
                "Scope",
                "All embedded-finance businesses");
        }

        AddCsvRow(csv, "Pricing", "Summary", null, "PolicyCount", assurance.Pricing.PolicyCount);
        AddCsvRow(csv, "Pricing", "Summary", null, "ActivePolicyCount", assurance.Pricing.ActivePolicyCount);
        AddCsvRow(csv, "Pricing", "Summary", null, "CompletedTrades", assurance.Pricing.CompletedTrades);
        AddCsvRow(csv, "Pricing", "Summary", null, "RealizedBusinessRevenue", assurance.Pricing.RealizedBusinessRevenue);

        foreach (var policy in assurance.Pricing.Policies)
        {
            var subject =
                $"{policy.SourceAssetCode}->{policy.DestinationAssetCode}";

            AddCsvRow(csv, "PricingPolicy", subject, policy.Id.ToString(), "Status", policy.IsActive ? "Active" : "Disabled");
            AddCsvRow(csv, "PricingPolicy", subject, policy.Id.ToString(), "AdjustmentType", policy.AdjustmentType);
            AddCsvRow(csv, "PricingPolicy", subject, policy.Id.ToString(), "AdjustmentValue", policy.AdjustmentValue);
            AddCsvRow(csv, "PricingPolicy", subject, policy.Id.ToString(), "MinimumCustomerRate", policy.MinimumCustomerRate);
            AddCsvRow(csv, "PricingPolicy", subject, policy.Id.ToString(), "MaximumCustomerRate", policy.MaximumCustomerRate);
            AddCsvRow(csv, "PricingPolicy", subject, policy.Id.ToString(), "EffectiveFromUtc", policy.EffectiveFrom);
            AddCsvRow(csv, "PricingPolicy", subject, policy.Id.ToString(), "EffectiveToUtc", policy.EffectiveTo);
        }

        foreach (var performance in assurance.Pricing.Performance)
        {
            var subject =
                $"{performance.SourceAssetCode}->{performance.DestinationAssetCode}";

            AddCsvRow(csv, "PricingPerformance", subject, null, "CompletedTrades", performance.CompletedTrades);
            AddCsvRow(csv, "PricingPerformance", subject, null, "SourceVolume", performance.SourceVolume);
            AddCsvRow(csv, "PricingPerformance", subject, null, "CustomerDestinationVolume", performance.CustomerDestinationVolume);
            AddCsvRow(csv, "PricingPerformance", subject, null, "BaseDestinationVolume", performance.BaseDestinationVolume);
            AddCsvRow(csv, "PricingPerformance", subject, null, "RealizedBusinessRevenue", performance.RealizedBusinessRevenue);
            AddCsvRow(csv, "PricingPerformance", subject, null, "AverageEffectiveMarkupPercentage", performance.AverageEffectiveMarkupPercentage);
        }

        AddCsvRow(csv, "Security", "Summary", null, "ApiApplications", assurance.Security.ApiApplications);
        AddCsvRow(csv, "Security", "Summary", null, "ActiveApiApplications", assurance.Security.ActiveApiApplications);
        AddCsvRow(csv, "Security", "Summary", null, "ApplicationsWithIpAllowlist", assurance.Security.ApplicationsWithIpAllowlist);
        AddCsvRow(csv, "Security", "Summary", null, "ApplicationsWithoutIpAllowlist", assurance.Security.ApplicationsWithoutIpAllowlist);
        AddCsvRow(csv, "Security", "Summary", null, "ActiveCredentials", assurance.Security.ActiveCredentials);
        AddCsvRow(csv, "Security", "Summary", null, "ExpiringCredentialsNext30Days", assurance.Security.ExpiringCredentialsNext30Days);
        AddCsvRow(csv, "Security", "Summary", null, "ExpiredCredentials", assurance.Security.ExpiredCredentials);
        AddCsvRow(csv, "Security", "Summary", null, "IdempotencyRecordsLast24Hours", assurance.Security.IdempotencyRecordsLast24Hours);
        AddCsvRow(csv, "Security", "Summary", null, "LatestIdempotencyAtUtc", assurance.Security.LatestIdempotencyAt);

        foreach (var application in assurance.Security.Applications)
        {
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "Status", application.Status);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "Scopes", application.Scopes);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "AllowedIpCount", application.AllowedIpCount);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "CredentialCount", application.CredentialCount);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "ActiveCredentialCount", application.ActiveCredentialCount);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "ExpiringCredentialCount", application.ExpiringCredentialCount);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "LastAuthenticatedAtUtc", application.LastAuthenticatedAt);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "LastCredentialUsedAtUtc", application.LastCredentialUsedAt);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "IdempotencyRecordsLast24Hours", application.IdempotencyRecordsLast24Hours);
            AddCsvRow(csv, "ApiApplication", application.Name, application.Id.ToString(), "LatestIdempotencyAtUtc", application.LatestIdempotencyAt);
        }

        AddCsvRow(csv, "Compliance", "Summary", null, "KybStatus", assurance.Compliance.KybStatus);
        AddCsvRow(csv, "Compliance", "Summary", null, "OpenCases", assurance.Compliance.OpenCases);
        AddCsvRow(csv, "Compliance", "Summary", null, "BlockingCases", assurance.Compliance.BlockingCases);
        AddCsvRow(csv, "Compliance", "Summary", null, "ScreeningsLast30Days", assurance.Compliance.ScreeningsLast30Days);
        AddCsvRow(csv, "Compliance", "Summary", null, "BlockingScreenings", assurance.Compliance.BlockingScreenings);
        AddCsvRow(csv, "Compliance", "Summary", null, "FailedScreeningsLast24Hours", assurance.Compliance.FailedScreeningsLast24Hours);
        AddCsvRow(csv, "Compliance", "Summary", null, "ActiveBusinessRestrictions", assurance.Compliance.ActiveBusinessRestrictions);
        AddCsvRow(csv, "Compliance", "Summary", null, "ActiveCustomerRestrictions", assurance.Compliance.ActiveCustomerRestrictions);
        AddCsvRow(csv, "Compliance", "Summary", null, "LatestScreeningAtUtc", assurance.Compliance.LatestScreeningAt);

        AddCsvRow(csv, "Operations", "Summary", null, "FailedProviderRequestsLast24Hours", assurance.Operations.FailedProviderRequestsLast24Hours);
        AddCsvRow(csv, "Operations", "Summary", null, "StaleProviderTransactions", assurance.Operations.StaleProviderTransactions);
        AddCsvRow(csv, "Operations", "Summary", null, "PendingProviderMappings", assurance.Operations.PendingProviderMappings);
        AddCsvRow(csv, "Operations", "Summary", null, "FailedProviderMappings", assurance.Operations.FailedProviderMappings);
        AddCsvRow(csv, "Operations", "Summary", null, "PendingWebhookDeliveries", assurance.Operations.PendingWebhookDeliveries);
        AddCsvRow(csv, "Operations", "Summary", null, "RetryWebhookDeliveries", assurance.Operations.RetryWebhookDeliveries);
        AddCsvRow(csv, "Operations", "Summary", null, "DeadLetterWebhookDeliveries", assurance.Operations.DeadLetterWebhookDeliveries);
        AddCsvRow(csv, "Operations", "Summary", null, "OldestPendingWebhookAtUtc", assurance.Operations.OldestPendingWebhookAt);
        AddCsvRow(csv, "Operations", "Summary", null, "ProviderMetricsScope", assurance.Operations.ProviderMetricsAreGlobal ? "Platform" : "Business");

        return new UTF8Encoding(false).GetBytes(csv.ToString());
    }

    private async Task<EmbeddedFinanceAdminPricingAssuranceDto> BuildPricingAsync(
        Guid? businessProfileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct)
    {
        if (!businessProfileId.HasValue)
        {
            return new EmbeddedFinanceAdminPricingAssuranceDto(
                false,
                0,
                0,
                0,
                0m,
                Array.Empty<EmbeddedFinanceAdminPricingPolicyDto>(),
                Array.Empty<EmbeddedFinanceAdminPricingPerformanceDto>());
        }

        var policies = await _pricing.GetPoliciesAsync(
            businessProfileId.Value,
            ct);

        var performance = await _pricing.GetPerformanceAsync(
            businessProfileId.Value,
            fromUtc,
            toUtc,
            ct);

        var policyRows = policies
            .Select(x => new EmbeddedFinanceAdminPricingPolicyDto(
                x.Id,
                x.SourceAssetCode,
                x.DestinationAssetCode,
                x.AdjustmentType.ToString(),
                x.AdjustmentValue,
                x.MinimumCustomerRate,
                x.MaximumCustomerRate,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.IsActive,
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToList();

        var performanceRows = performance
            .Select(x => new EmbeddedFinanceAdminPricingPerformanceDto(
                x.SourceAssetCode,
                x.DestinationAssetCode,
                x.CompletedTrades,
                x.SourceVolume,
                x.CustomerDestinationVolume,
                x.BaseDestinationVolume,
                x.RealizedBusinessRevenue,
                x.AverageEffectiveMarkupPercentage))
            .ToList();

        return new EmbeddedFinanceAdminPricingAssuranceDto(
            true,
            policyRows.Count,
            policyRows.Count(x => x.IsActive),
            performanceRows.Sum(x => x.CompletedTrades),
            performanceRows.Sum(x => x.RealizedBusinessRevenue),
            policyRows,
            performanceRows);
    }

    private async Task<EmbeddedFinanceAdminSecurityAssuranceDto> BuildSecurityAsync(
        Guid? businessProfileId,
        DateTime now,
        DateTime last24Hours,
        DateTime credentialExpiryBefore,
        CancellationToken ct)
    {
        var applications = _db.ApiApplications
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (businessProfileId.HasValue)
        {
            applications = applications.Where(
                x => x.BusinessProfileId == businessProfileId.Value);
        }

        var rawRows = await applications
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Status,
                x.Scopes,
                x.AllowedIpRanges,
                CredentialCount = x.Credentials.Count(c => !c.IsDeleted),
                ActiveCredentialCount = x.Credentials.Count(c =>
                    !c.IsDeleted &&
                    c.Status == ApiCredentialStatus.Active &&
                    (!c.ExpiresAt.HasValue || c.ExpiresAt.Value > now)),
                ExpiringCredentialCount = x.Credentials.Count(c =>
                    !c.IsDeleted &&
                    c.Status == ApiCredentialStatus.Active &&
                    c.ExpiresAt.HasValue &&
                    c.ExpiresAt.Value > now &&
                    c.ExpiresAt.Value <= credentialExpiryBefore),
                x.LastAuthenticatedAt,
                LastCredentialUsedAt = x.Credentials
                    .Where(c => !c.IsDeleted && c.LastUsedAt.HasValue)
                    .OrderByDescending(c => c.LastUsedAt)
                    .Select(c => c.LastUsedAt)
                    .FirstOrDefault(),
                IdempotencyRecordsLast24Hours = x.IdempotencyRecords.Count(r =>
                    !r.IsDeleted &&
                    r.CreatedAt >= last24Hours),
                LatestIdempotencyAt = x.IdempotencyRecords
                    .Where(r => !r.IsDeleted)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => (DateTime?)r.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var rows = rawRows
            .Select(x => new EmbeddedFinanceAdminApplicationSecurityDto(
                x.Id,
                x.Name,
                x.Status.ToString(),
                x.Scopes,
                CountCsvValues(x.AllowedIpRanges),
                x.CredentialCount,
                x.ActiveCredentialCount,
                x.ExpiringCredentialCount,
                x.LastAuthenticatedAt,
                x.LastCredentialUsedAt,
                x.IdempotencyRecordsLast24Hours,
                x.LatestIdempotencyAt))
            .ToList();

        var credentials = _db.ApiCredentials
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                !x.ApiApplication.IsDeleted);

        var idempotency = _db.EmbeddedApiIdempotencyRecords
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                !x.ApiApplication.IsDeleted);

        if (businessProfileId.HasValue)
        {
            credentials = credentials.Where(
                x => x.ApiApplication.BusinessProfileId == businessProfileId.Value);

            idempotency = idempotency.Where(
                x => x.ApiApplication.BusinessProfileId == businessProfileId.Value);
        }

        return new EmbeddedFinanceAdminSecurityAssuranceDto(
            rows.Count,
            rawRows.Count(x => x.Status == ApiApplicationStatus.Active),
            rawRows.Count(x => CountCsvValues(x.AllowedIpRanges) > 0),
            rawRows.Count(x => CountCsvValues(x.AllowedIpRanges) == 0),
            await credentials.CountAsync(
                x =>
                    x.Status == ApiCredentialStatus.Active &&
                    (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now),
                ct),
            await credentials.CountAsync(
                x =>
                    x.Status == ApiCredentialStatus.Active &&
                    x.ExpiresAt.HasValue &&
                    x.ExpiresAt.Value > now &&
                    x.ExpiresAt.Value <= credentialExpiryBefore,
                ct),
            await credentials.CountAsync(
                x =>
                    x.Status == ApiCredentialStatus.Expired ||
                    (x.Status == ApiCredentialStatus.Active &&
                     x.ExpiresAt.HasValue &&
                     x.ExpiresAt.Value <= now),
                ct),
            await idempotency.CountAsync(
                x => x.CreatedAt >= last24Hours,
                ct),
            await idempotency
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(ct),
            rows);
    }

    private async Task<EmbeddedFinanceAdminComplianceAssuranceDto> BuildComplianceAsync(
        Guid? businessProfileId,
        string? kybStatus,
        DateTime last24Hours,
        DateTime last30Days,
        CancellationToken ct)
    {
        var cases = _db.ComplianceCases
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var screenings = _db.ScreeningRecords
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (businessProfileId.HasValue)
        {
            cases = cases.Where(
                x => x.BusinessProfileId == businessProfileId.Value);

            screenings = screenings.Where(
                x => x.BusinessProfileId == businessProfileId.Value);
        }

        var activeBusinessRestrictions = _db.OutboundFundsRestrictions
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.SubjectType ==
                    OutboundFundsRestrictionSubjectType.BusinessProfile);

        var activeCustomerRestrictions = _db.OutboundFundsRestrictions
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.SubjectType ==
                    OutboundFundsRestrictionSubjectType.BusinessCustomer);

        if (businessProfileId.HasValue)
        {
            activeBusinessRestrictions = activeBusinessRestrictions.Where(
                x => x.SubjectId == businessProfileId.Value);

            activeCustomerRestrictions = activeCustomerRestrictions.Where(
                x => _db.BusinessCustomers.Any(c =>
                    !c.IsDeleted &&
                    c.Id == x.SubjectId &&
                    c.BusinessProfileId == businessProfileId.Value));
        }

        return new EmbeddedFinanceAdminComplianceAssuranceDto(
            kybStatus,
            await cases.CountAsync(
                x =>
                    x.Status != ComplianceCaseStatus.Resolved &&
                    x.Status != ComplianceCaseStatus.Closed,
                ct),
            await cases.CountAsync(
                x =>
                    x.IsBlocking &&
                    x.Status != ComplianceCaseStatus.Resolved &&
                    x.Status != ComplianceCaseStatus.Closed,
                ct),
            await screenings.CountAsync(
                x => x.ScreenedAt >= last30Days,
                ct),
            await screenings.CountAsync(
                x => x.IsBlocking,
                ct),
            await screenings.CountAsync(
                x =>
                    x.Status == ScreeningStatus.Failed &&
                    x.ScreenedAt >= last24Hours,
                ct),
            await activeBusinessRestrictions.CountAsync(ct),
            await activeCustomerRestrictions.CountAsync(ct),
            await screenings
                .OrderByDescending(x => x.ScreenedAt)
                .Select(x => (DateTime?)x.ScreenedAt)
                .FirstOrDefaultAsync(ct));
    }

    private async Task<EmbeddedFinanceAdminOperationalAssuranceDto> BuildOperationsAsync(
        Guid? businessProfileId,
        DateTime last24Hours,
        DateTime staleProviderBefore,
        CancellationToken ct)
    {
        var mappings = _db.ProviderAccountMappings
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var deliveries = _db.BusinessWebhookDeliveries
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                !x.BusinessWebhookEndpoint.IsDeleted);

        if (businessProfileId.HasValue)
        {
            mappings = mappings.Where(
                x => x.CollectionAccount.BusinessProfileId ==
                    businessProfileId.Value);

            deliveries = deliveries.Where(
                x => x.BusinessWebhookEndpoint.BusinessProfileId ==
                    businessProfileId.Value);
        }

        return new EmbeddedFinanceAdminOperationalAssuranceDto(
            await _db.ProviderRequestLogs.CountAsync(
                x =>
                    !x.IsDeleted &&
                    x.Status == ProviderRequestStatus.Failed &&
                    x.RequestedAt >= last24Hours,
                ct),
            await _db.ProviderTransactions.CountAsync(
                x =>
                    !x.IsDeleted &&
                    x.LastSyncedAt < staleProviderBefore,
                ct),
            await mappings.CountAsync(
                x => x.Status == ProviderAccountMappingStatus.Pending,
                ct),
            await mappings.CountAsync(
                x => x.Status == ProviderAccountMappingStatus.Failed,
                ct),
            await deliveries.CountAsync(
                x => x.Status == BusinessWebhookDeliveryStatus.Pending,
                ct),
            await deliveries.CountAsync(
                x => x.Status == BusinessWebhookDeliveryStatus.Retry,
                ct),
            await deliveries.CountAsync(
                x => x.Status == BusinessWebhookDeliveryStatus.DeadLetter,
                ct),
            await deliveries
                .Where(x => x.Status == BusinessWebhookDeliveryStatus.Pending)
                .OrderBy(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(ct),
            true);
    }

    private static void AddCsvRow(
        StringBuilder csv,
        string section,
        string subject,
        string? identifier,
        string metric,
        object? value)
    {
        csv.Append(CsvCell(section));
        csv.Append(',');
        csv.Append(CsvCell(subject));
        csv.Append(',');
        csv.Append(CsvCell(identifier));
        csv.Append(',');
        csv.Append(CsvCell(metric));
        csv.Append(',');
        csv.Append(CsvCell(value));
        csv.AppendLine();
    }

    private static string CsvCell(object? value)
    {
        var protectAsText = value is string;

        var text = value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime
                .ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset
                .ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

        if (protectAsText &&
            text.Length > 0 &&
            (text[0] == '=' ||
             text[0] == '+' ||
             text[0] == '-' ||
             text[0] == '@'))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static int CountCsvValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Length;
}
