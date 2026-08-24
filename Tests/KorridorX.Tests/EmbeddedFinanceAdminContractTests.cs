using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Dtos.EmbeddedFinance;
using Microsoft.AspNetCore.Authorization;

namespace KorridorX.Tests;

public sealed class EmbeddedFinanceAdminContractTests
{
    [Fact]
    public void Controller_allows_read_access_for_operations_and_compliance_roles()
    {
        var attribute = typeof(AdminEmbeddedFinanceController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(
            "Compliance,Operations,Admin,SuperAdmin",
            attribute!.Roles);
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
        Assert.DoesNotContain("SigningSecretProtected", properties);
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
