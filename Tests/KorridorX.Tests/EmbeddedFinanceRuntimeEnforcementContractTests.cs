using System.Reflection;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;

namespace KorridorX.Tests;

public sealed class EmbeddedFinanceRuntimeEnforcementContractTests
{
    [Fact]
    public void Rate_limiter_precedes_embedded_api_key_authentication()
    {
        var source = ReadRepositoryFile("Program.cs");
        var rateLimiter = source.IndexOf(
            "app.UseRateLimiter();",
            StringComparison.Ordinal);
        var apiKeyMiddleware = source.IndexOf(
            "app.UseMiddleware<EmbeddedFinanceApiKeyMiddleware>();",
            StringComparison.Ordinal);

        Assert.True(rateLimiter >= 0);
        Assert.True(apiKeyMiddleware >= 0);
        Assert.True(
            rateLimiter < apiKeyMiddleware,
            "Rejected Embedded Finance API keys must pass through rate limiting before credential verification.");
    }

    [Fact]
    public void Business_credential_status_uses_effective_expiry()
    {
        var method = typeof(EmbeddedFinanceManagementService)
            .GetMethod(
                "EffectiveCredentialStatus",
                BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var now = DateTime.UtcNow;

        var expired = Assert.IsType<ApiCredentialStatus>(
            method!.Invoke(
                null,
                new object?[]
                {
                    ApiCredentialStatus.Active,
                    now.AddMinutes(-1),
                    now
                }));

        var active = Assert.IsType<ApiCredentialStatus>(
            method.Invoke(
                null,
                new object?[]
                {
                    ApiCredentialStatus.Active,
                    now.AddMinutes(1),
                    now
                }));

        var revoked = Assert.IsType<ApiCredentialStatus>(
            method.Invoke(
                null,
                new object?[]
                {
                    ApiCredentialStatus.Revoked,
                    now.AddMinutes(-1),
                    now
                }));

        Assert.Equal(ApiCredentialStatus.Expired, expired);
        Assert.Equal(ApiCredentialStatus.Active, active);
        Assert.Equal(ApiCredentialStatus.Revoked, revoked);
    }

    [Fact]
    public void Webhook_staging_and_final_send_require_active_api_application()
    {
        var stager = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedWebhookOutboxStager.cs"));

        var sender = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedWebhookSender.cs"));

        Assert.Contains(
            "x.ApiApplication.Status == ApiApplicationStatus.Active",
            stager,
            StringComparison.Ordinal);

        Assert.Contains(
            "endpoint.ApiApplication.Status != ApiApplicationStatus.Active",
            sender,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Webhook_retry_and_enable_paths_require_active_application()
    {
        var business = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedWebhookManagementService.cs"));

        var admin = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedFinanceAdminCommandService.cs"));

        Assert.Contains(
            "Webhook endpoint cannot be enabled while its API application is not active.",
            business,
            StringComparison.Ordinal);

        Assert.Contains(
            "Webhook delivery cannot be retried while its API application is not active.",
            business,
            StringComparison.Ordinal);

        Assert.Contains(
            "Webhook endpoint cannot be enabled while its API application is not active.",
            admin,
            StringComparison.Ordinal);

        Assert.Contains(
            "Webhook delivery cannot be retried while its API application is not active.",
            admin,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Suspended_and_closed_collection_accounts_cannot_be_publicly_provisioned()
    {
        var method = typeof(CollectionAccountProvisioningService)
            .GetMethod(
                "EnsureProvisionableAccountStatus",
                BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        method!.Invoke(
            null,
            new object?[] { CollectionAccountStatus.Pending });

        AssertLifecycleRejected(
            method,
            CollectionAccountStatus.Suspended,
            "Reactivate the collection account");

        AssertLifecycleRejected(
            method,
            CollectionAccountStatus.Closed,
            "closed collection account");
    }

    [Fact]
    public void Idempotent_replays_are_bound_to_expected_resource_type()
    {
        var customer = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedFinanceCustomerService.cs"));
        var payout = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedFinancePayoutService.cs"));
        var transfer = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedFinanceTransferService.cs"));

        Assert.Contains(
            "nameof(BusinessCustomer)",
            customer,
            StringComparison.Ordinal);
        Assert.Contains(
            "nameof(CollectionAccount)",
            customer,
            StringComparison.Ordinal);
        Assert.Contains(
            "nameof(Payout)",
            payout,
            StringComparison.Ordinal);
        Assert.Contains(
            "nameof(Transfer)",
            transfer,
            StringComparison.Ordinal);

        foreach (var source in new[] { customer, payout, transfer })
        {
            Assert.Contains(
                "The Idempotency-Key has already been used for a different operation.",
                source,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Suspended_webhook_application_pauses_without_consuming_attempt()
    {
        var worker = ReadRepositoryFile(
            Path.Combine(
                "BackgroundJobs",
                "EmbeddedWebhookDeliveryWorker.cs"));

        var suspended = worker.IndexOf(
            "application.Status == ApiApplicationStatus.Suspended",
            StringComparison.Ordinal);
        var attemptIncrement = worker.IndexOf(
            "delivery.AttemptCount += 1;",
            StringComparison.Ordinal);

        Assert.True(suspended >= 0);
        Assert.True(attemptIncrement >= 0);
        Assert.True(
            suspended < attemptIncrement,
            "Suspended applications must be paused before a delivery attempt is consumed.");

        Assert.Contains(
            "delivery.NextAttemptAt = now.AddMinutes(5);",
            worker,
            StringComparison.Ordinal);
        Assert.Contains(
            "application.Status == ApiApplicationStatus.Disabled",
            worker,
            StringComparison.Ordinal);
        Assert.Contains(
            "Webhook API application is disabled.",
            worker,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Provisioning_is_single_flight_and_revalidates_state_before_dispatch()
    {
        var publicProvisioning = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "CollectionAccountProvisioningService.cs"));
        var adminProvisioning = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedFinanceAdminCommandService.cs"));

        Assert.Contains(
            "existing?.Status == ProviderAccountMappingStatus.Pending",
            publicProvisioning,
            StringComparison.Ordinal);

        var publicRecheck = publicProvisioning.IndexOf(
            "var dispatchState = await _db.CollectionAccounts",
            StringComparison.Ordinal);
        var publicProviderCall = publicProvisioning.IndexOf(
            "var result = await provisioner.ProvisionAsync(",
            StringComparison.Ordinal);

        Assert.True(publicRecheck >= 0);
        Assert.True(publicProviderCall >= 0);
        Assert.True(publicRecheck < publicProviderCall);

        Assert.Contains(
            "mappingStillPending",
            publicProvisioning,
            StringComparison.Ordinal);
        Assert.Contains(
            "CustomerStatus = x.BusinessCustomer.Status",
            publicProvisioning,
            StringComparison.Ordinal);

        var adminRecheck = adminProvisioning.IndexOf(
            "var dispatchState = await _db.ProviderAccountMappings",
            StringComparison.Ordinal);
        var adminProviderCall = adminProvisioning.IndexOf(
            "var result = await provisioner.ProvisionAsync(",
            StringComparison.Ordinal);

        Assert.True(adminRecheck >= 0);
        Assert.True(adminProviderCall >= 0);
        Assert.True(adminRecheck < adminProviderCall);
        Assert.Contains(
            "MappingStatus = x.Status",
            adminProvisioning,
            StringComparison.Ordinal);
        Assert.Contains(
            "CustomerStatus = x.CollectionAccount.BusinessCustomer.Status",
            adminProvisioning,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_api_authentication_and_scope_boundary_remains_exact()
    {
        var authenticator = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedFinanceCredentialAuthenticator.cs"));
        var context = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "IEmbeddedFinanceContextAccessor.cs"));

        Assert.Contains(
            "credential.Status != ApiCredentialStatus.Active",
            authenticator,
            StringComparison.Ordinal);
        Assert.Contains(
            "credential.ApiApplication.Status != ApiApplicationStatus.Active",
            authenticator,
            StringComparison.Ordinal);
        Assert.Contains(
            "credential.ExpiresAt.HasValue && credential.ExpiresAt <= now",
            authenticator,
            StringComparison.Ordinal);
        Assert.Contains(
            "public bool HasScope(EmbeddedFinanceScope scope) => (Scopes & scope) == scope;",
            context,
            StringComparison.Ordinal);

        var principal = new EmbeddedFinancePrincipal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EmbeddedFinanceScope.CustomersRead |
            EmbeddedFinanceScope.AccountsRead,
            "test-key");

        Assert.True(principal.HasScope(EmbeddedFinanceScope.CustomersRead));
        Assert.True(principal.HasScope(EmbeddedFinanceScope.AccountsRead));
        Assert.False(principal.HasScope(EmbeddedFinanceScope.CustomersWrite));
        Assert.False(principal.HasScope(EmbeddedFinanceScope.PayoutsWrite));
    }

    [Fact]
    public void Provider_money_out_paths_have_final_outbound_restriction_gate()
    {
        var canonicalPayout = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "Payments",
                "PayoutService.cs"));

        var embeddedPayout = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "EmbeddedFinance",
                "EmbeddedFinancePayoutService.cs"));

        var digitalAssets = ReadRepositoryFile(
            Path.Combine(
                "Services",
                "DigitalAssets",
                "DigitalAssetService.cs"));

        AssertFinalOutboundGate(
            canonicalPayout,
            "await EnsureTransferOutboundRestrictionAsync(transfer, ct);",
            "var result = await _remittanceProvider.InitiatePayoutAsync(");

        AssertFinalOutboundGate(
            embeddedPayout,
            "await _outboundFundsRestrictions.EnsureBusinessCustomerOutboundAllowedAsync(",
            "var result = await _provider.InitiatePayoutAsync(");

        AssertFinalOutboundGate(
            digitalAssets,
            "await _outboundFundsRestrictions.EnsureBusinessCustomerOutboundAllowedAsync(",
            "var submission = await provider.SubmitWithdrawalAsync(");
    }

    private static void AssertFinalOutboundGate(
        string source,
        string restrictionMarker,
        string providerCallMarker)
    {
        var providerCall = source.IndexOf(
            providerCallMarker,
            StringComparison.Ordinal);

        Assert.True(
            providerCall >= 0,
            $"Provider call marker was not found: {providerCallMarker}");

        var restriction = source.LastIndexOf(
            restrictionMarker,
            providerCall,
            StringComparison.Ordinal);

        Assert.True(
            restriction >= 0,
            $"Final outbound restriction marker was not found before provider dispatch: {restrictionMarker}");

        var between = source.Substring(
            restriction,
            providerCall - restriction);

        Assert.DoesNotContain(
            "SaveChangesAsync",
            between,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "CommitAsync",
            between,
            StringComparison.Ordinal);
    }

    private static void AssertLifecycleRejected(
        MethodInfo method,
        CollectionAccountStatus status,
        string expectedMessage)
    {
        var exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, new object?[] { status }));

        var inner = Assert.IsType<InvalidOperationException>(
            exception.InnerException);

        Assert.Contains(
            expectedMessage,
            inner.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var project = Path.Combine(
                directory.FullName,
                "KorridorX.csproj");

            if (File.Exists(project))
            {
                var path = Path.Combine(
                    directory.FullName,
                    relativePath);

                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        $"Repository contract file was not found: {path}");

                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "KorridorX repository root could not be located from the test output directory.");
    }
}
