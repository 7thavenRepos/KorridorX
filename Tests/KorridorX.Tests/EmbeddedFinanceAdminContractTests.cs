using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Tests;

public sealed class EmbeddedFinanceAdminContractTests
{
    private const string ReadRoles =
        "Compliance,InternalAudit,Operations,Admin,SuperAdmin";

    private const string MutationRoles =
        "Operations,Admin,SuperAdmin";

    [Fact]
    public void Controller_read_contract_includes_internal_audit()
    {
        var attribute = typeof(AdminEmbeddedFinanceController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(ReadRoles, attribute!.Roles);
    }

    [Fact]
    public void Every_admin_mutation_is_operations_only_and_requires_reason()
    {
        var expectedRoutes = new[]
        {
            "accounts/{collectionAccountId:guid}/provider-mappings/{providerMappingId:guid}/retry",
            "accounts/{collectionAccountId:guid}/status",
            "applications/{apiApplicationId:guid}/credentials/{credentialId:guid}/revoke",
            "applications/{apiApplicationId:guid}/status",
            "customers/{businessCustomerId:guid}/status",
            "webhook-deliveries/{deliveryId:guid}/retry",
            "webhook-endpoints/{endpointId:guid}/status"
        };

        var methods = typeof(AdminEmbeddedFinanceController)
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Select(x => new
            {
                Method = x,
                Route = x.GetCustomAttribute<HttpPostAttribute>()
            })
            .Where(x => x.Route is not null)
            .ToList();

        Assert.Equal(
            expectedRoutes.OrderBy(x => x).ToArray(),
            methods
                .Select(x => x.Route!.Template!)
                .OrderBy(x => x)
                .ToArray());

        foreach (var item in methods)
        {
            var authorize = item.Method
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal(MutationRoles, authorize!.Roles);

            var reasonRequest = item.Method
                .GetParameters()
                .FirstOrDefault(x =>
                    x.ParameterType
                        .GetProperty("Reason")?
                        .PropertyType == typeof(string));

            Assert.NotNull(reasonRequest);
        }
    }

    [Fact]
    public void Read_only_investigation_and_export_routes_are_stable()
    {
        var expectedRoutes = new[]
        {
            "accounts",
            "accounts/{collectionAccountId:guid}/provider-mappings",
            "activity",
            "activity.csv",
            "activity/{activityType}/{activityId:guid}",
            "applications",
            "applications/{apiApplicationId:guid}/credentials",
            "assurance",
            "assurance.csv",
            "businesses",
            "customers",
            "exceptions",
            "overview",
            "webhook-deliveries",
            "webhook-endpoints"
        };

        var methods = typeof(AdminEmbeddedFinanceController)
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Select(x => new
            {
                Method = x,
                Route = x.GetCustomAttribute<HttpGetAttribute>()
            })
            .Where(x => x.Route is not null)
            .ToList();

        Assert.Equal(
            expectedRoutes.OrderBy(x => x).ToArray(),
            methods
                .Select(x => x.Route!.Template!)
                .OrderBy(x => x)
                .ToArray());

        foreach (var item in methods)
        {
            Assert.Null(
                item.Method.GetCustomAttribute<AuthorizeAttribute>());

            Assert.Null(
                item.Method.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }

    [Fact]
    public void Credential_admin_contract_never_exposes_api_key_or_secret_hash()
    {
        var properties = typeof(EmbeddedFinanceAdminCredentialDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("SecretLastFour", properties);
        Assert.DoesNotContain("ApiKey", properties);
        Assert.DoesNotContain("SecretHash", properties);
        Assert.DoesNotContain("Secret", properties);
    }

    [Fact]
    public void Webhook_endpoint_admin_contract_exposes_only_secret_last_four()
    {
        var properties = typeof(EmbeddedFinanceAdminWebhookEndpointDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("SigningSecretLastFour", properties);
        Assert.DoesNotContain("SigningSecret", properties);
        Assert.DoesNotContain(
            "SigningSecretProtected",
            properties);
    }

    [Fact]
    public void Assurance_contract_does_not_expose_protected_material()
    {
        var assuranceTypes = new[]
        {
            typeof(EmbeddedFinanceAdminAssuranceDto),
            typeof(EmbeddedFinanceAdminAssuranceBusinessDto),
            typeof(EmbeddedFinanceAdminPricingAssuranceDto),
            typeof(EmbeddedFinanceAdminPricingPolicyDto),
            typeof(EmbeddedFinanceAdminPricingPerformanceDto),
            typeof(EmbeddedFinanceAdminSecurityAssuranceDto),
            typeof(EmbeddedFinanceAdminApplicationSecurityDto),
            typeof(EmbeddedFinanceAdminComplianceAssuranceDto),
            typeof(EmbeddedFinanceAdminOperationalAssuranceDto)
        };

        var forbiddenProperties = new[]
        {
            "ApiKey",
            "Secret",
            "SecretHash",
            "SigningSecret",
            "SigningSecretProtected",
            "IdempotencyKey",
            "RequestBody",
            "ResponseBody",
            "HeadersJson",
            "OldValuesJson",
            "NewValuesJson"
        };

        foreach (var type in assuranceTypes)
        {
            foreach (var property in type.GetProperties())
            {
                Assert.False(
                    forbiddenProperties.Contains(
                        property.Name,
                        StringComparer.OrdinalIgnoreCase),
                    $"{type.Name}.{property.Name} must not be exposed by the assurance contract.");
            }
        }
    }

    [Fact]
    public void Assurance_csv_neutralizes_spreadsheet_formula_prefixes()
    {
        var csvCell = typeof(
                EmbeddedFinanceAdminAssuranceService)
            .GetMethod(
                "CsvCell",
                BindingFlags.NonPublic |
                BindingFlags.Static);

        Assert.NotNull(csvCell);

        string Render(string value) =>
            Assert.IsType<string>(
                csvCell!.Invoke(
                    null,
                    new object?[] { value }));

        Assert.Equal("\"'=2+3\"", Render("=2+3"));
        Assert.Equal("\"'+2\"", Render("+2"));
        Assert.Equal("\"'-2\"", Render("-2"));
        Assert.Equal("\"'@SUM(A1:A2)\"", Render("@SUM(A1:A2)"));
        Assert.Equal("\"safe\"", Render("safe"));
    }

    [Fact]
    public void Sensitive_command_request_contracts_all_require_internal_reason()
    {
        var requestTypes = new[]
        {
            typeof(EmbeddedFinanceAdminApplicationStatusRequestDto),
            typeof(EmbeddedFinanceAdminReasonRequestDto),
            typeof(EmbeddedFinanceAdminWebhookStatusRequestDto),
            typeof(EmbeddedFinanceAdminCustomerStatusRequestDto),
            typeof(EmbeddedFinanceAdminCollectionAccountStatusRequestDto)
        };

        foreach (var type in requestTypes)
        {
            var reason = type.GetProperty("Reason");

            Assert.NotNull(reason);
            Assert.Equal(typeof(string), reason!.PropertyType);
        }
    }

    [Fact]
    public void Disabled_api_application_is_terminal_and_cannot_be_reactivated()
    {
        var transition = typeof(EmbeddedFinanceAdminCommandService)
            .GetMethod(
                "EnsureApplicationStatusTransition",
                BindingFlags.NonPublic |
                BindingFlags.Static);

        Assert.NotNull(transition);

        transition!.Invoke(
            null,
            new object?[]
            {
                ApiApplicationStatus.Suspended,
                ApiApplicationStatus.Active
            });

        var exception = Assert.Throws<TargetInvocationException>(
            () => transition.Invoke(
                null,
                new object?[]
                {
                    ApiApplicationStatus.Disabled,
                    ApiApplicationStatus.Active
                }));

        Assert.IsType<InvalidOperationException>(
            exception.InnerException);
    }

    [Fact]
    public void Expired_active_credential_is_reported_as_expired()
    {
        var effectiveStatus = typeof(EmbeddedFinanceAdminQueryService)
            .GetMethod(
                "EffectiveCredentialStatus",
                BindingFlags.NonPublic |
                BindingFlags.Static);

        Assert.NotNull(effectiveStatus);

        var now = DateTime.UtcNow;

        var expired = effectiveStatus!.Invoke(
            null,
            new object?[]
            {
                ApiCredentialStatus.Active,
                now.AddMinutes(-1),
                now
            });

        var active = effectiveStatus.Invoke(
            null,
            new object?[]
            {
                ApiCredentialStatus.Active,
                now.AddMinutes(1),
                now
            });

        Assert.Equal(
            ApiCredentialStatus.Expired,
            Assert.IsType<ApiCredentialStatus>(expired));

        Assert.Equal(
            ApiCredentialStatus.Active,
            Assert.IsType<ApiCredentialStatus>(active));
    }

    [Fact]
    public void Internal_audit_has_linked_audit_api_read_access()
    {
        var attribute = typeof(AdminAuditController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);

        var roles = (attribute!.Roles ?? string.Empty)
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        Assert.Contains("InternalAudit", roles);
    }

    [Fact]
    public void Overview_contract_tracks_dead_letter_and_retry_queues()
    {
        var overview = new EmbeddedFinanceAdminOverviewDto(
            Businesses: 1,
            ApiApplications: 2,
            ActiveApiApplications: 1,
            ActiveCredentials: 3,
            BusinessCustomers: 4,
            ActiveBusinessCustomers: 3,
            CollectionAccounts: 5,
            ActiveCollectionAccounts: 4,
            WebhookEndpoints: 2,
            ActiveWebhookEndpoints: 1,
            PendingWebhookDeliveries: 6,
            RetryWebhookDeliveries: 7,
            DeadLetterWebhookDeliveries: 8);

        Assert.Equal(7, overview.RetryWebhookDeliveries);
        Assert.Equal(8, overview.DeadLetterWebhookDeliveries);
    }
}
