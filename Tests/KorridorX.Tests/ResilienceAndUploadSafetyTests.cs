using System.Net;
using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Finance;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Finance;
using KorridorX.Services.Treasury;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ResilienceAndUploadSafetyTests
{
    private const long TenMegabytes = 10L * 1024 * 1024;
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ResilienceAndUploadSafetyTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Settlement_statement_rejects_files_over_10_mb_before_database_work()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var request = new ImportSettlementStatementFormDto
        {
            File = SizedFile(
                "statement.csv",
                TenMegabytes + 1)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            treasury.ImportSettlementStatementAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                request));

        Assert.Contains("cannot exceed 10 MB", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Settlement_statement_rejects_non_csv_files_before_database_work()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var request = new ImportSettlementStatementFormDto
        {
            File = SizedFile("statement.xlsx", 1)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            treasury.ImportSettlementStatementAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                request));

        Assert.Contains("must be a CSV file", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_invoice_rejects_files_over_10_mb_before_database_work()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var finance = scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

        var request = InvoiceRequest(
            $"P15B-{Guid.NewGuid():N}",
            SizedFile("invoice.csv", TenMegabytes + 1));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.ImportProviderInvoiceAsync(
                Guid.NewGuid(),
                request));

        Assert.Contains("cannot exceed 10 MB", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_invoice_rejects_non_csv_files_before_database_work()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var finance = scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

        var request = InvoiceRequest(
            $"P15B-{Guid.NewGuid():N}",
            SizedFile("invoice.pdf", 1));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.ImportProviderInvoiceAsync(
                Guid.NewGuid(),
                request));

        Assert.Contains("must be a CSV file", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_invoice_rejects_malformed_csv_missing_required_columns()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var finance = scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

        var request = InvoiceRequest(
            $"P15B-{Guid.NewGuid():N}",
            TextFile(
                "invoice.csv",
                "description\nProvider fee"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.ImportProviderInvoiceAsync(
                Guid.NewGuid(),
                request));

        Assert.Contains("missing required column", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net_amount", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_invoice_duplicate_file_hash_is_rejected_even_with_new_invoice_number()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var finance = scope.ServiceProvider.GetRequiredService<IFinancialCloseService>();

        var csv =
            "description,net_amount,tax_amount,total_amount\n" +
            "Provider fee,10.00,1.30,11.30";

        var first = InvoiceRequest(
            $"P15B-A-{Guid.NewGuid():N}",
            TextFile("invoice-a.csv", csv));

        await finance.ImportProviderInvoiceAsync(
            Guid.NewGuid(),
            first);

        var second = InvoiceRequest(
            $"P15B-B-{Guid.NewGuid():N}",
            TextFile("invoice-b.csv", csv));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finance.ImportProviderInvoiceAsync(
                Guid.NewGuid(),
                second));

        Assert.Contains(
            "file has already been imported",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Outbound_business_webhook_http_failure_is_returned_as_retryable_result_not_thrown()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();

        var sender = await CreateWebhookSenderAsync(
            scope,
            _ => throw new HttpRequestException("provider unavailable"));

        var result = await sender.Sender.SendAsync(sender.DeliveryId);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains(
            "provider unavailable",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Outbound_business_webhook_timeout_is_returned_as_retryable_result_not_thrown()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();

        var sender = await CreateWebhookSenderAsync(
            scope,
            _ => throw new TaskCanceledException("request timed out"));

        var result = await sender.Sender.SendAsync(sender.DeliveryId);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains(
            "timed out",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Outbound_business_webhook_failure_body_is_bounded_to_4000_characters()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();

        var sender = await CreateWebhookSenderAsync(
            scope,
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(new string('x', 5000))
            });

        var result = await sender.Sender.SendAsync(sender.DeliveryId);

        Assert.False(result.Success);
        Assert.Equal(
            (int)HttpStatusCode.ServiceUnavailable,
            result.StatusCode);
        Assert.NotNull(result.ResponseBody);
        Assert.Equal(4000, result.ResponseBody!.Length);
        Assert.Contains(
            "503",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<WebhookSenderFixture> CreateWebhookSenderAsync(
        AsyncServiceScope scope,
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dataProtection =
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();

        using var client = _fixture.CreateClient();
        var unique = Guid.NewGuid().ToString("N");

        var registration = await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Webhook",
                "Owner",
                $"phase15b-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550199",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var profile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"Phase 15B {unique}",
            CountryCode = "CA",
            ContactEmail = $"phase15b-{unique}@example.test",
            KybStatus = KybStatus.Approved,
            KybApprovedAt = DateTime.UtcNow
        };

        var application = new ApiApplication
        {
            BusinessProfile = profile,
            BusinessProfileId = profile.Id,
            Name = $"Phase15B-{unique}",
            Scopes = EmbeddedFinanceScope.WebhooksManage,
            Status = ApiApplicationStatus.Active
        };

        var secret = $"whsec_phase15b_{unique}";
        var protectedSecret = dataProtection
            .CreateProtector(
                "KorridorX.EmbeddedFinance.WebhookSigningSecret.v1")
            .Protect(secret);

        var endpoint = new BusinessWebhookEndpoint
        {
            BusinessProfileId = profile.Id,
            ApiApplication = application,
            ApiApplicationId = application.Id,
            Url = $"https://webhook.example.test/{unique}",
            EventTypesCsv = "transfer.completed",
            SigningSecretProtected = protectedSecret,
            SigningSecretLastFour = secret[^4..],
            Status = BusinessWebhookEndpointStatus.Active,
            MaxAttempts = 3
        };

        var webhookEvent = new BusinessWebhookEvent
        {
            BusinessProfileId = profile.Id,
            EventId = $"evt_{unique}",
            EventType = "transfer.completed",
            PayloadJson =
                $$"""{"eventId":"evt_{{unique}}","type":"transfer.completed"}""",
            OccurredAt = DateTime.UtcNow
        };

        var delivery = new BusinessWebhookDelivery
        {
            BusinessWebhookEndpoint = endpoint,
            BusinessWebhookEndpointId = endpoint.Id,
            BusinessWebhookEvent = webhookEvent,
            BusinessWebhookEventId = webhookEvent.Id,
            Status = BusinessWebhookDeliveryStatus.Pending,
            NextAttemptAt = DateTime.UtcNow
        };

        db.BusinessProfiles.Add(profile);
        db.ApiApplications.Add(application);
        db.BusinessWebhookEndpoints.Add(endpoint);
        db.BusinessWebhookEvents.Add(webhookEvent);
        db.BusinessWebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var httpClient = new HttpClient(
            new StubHttpMessageHandler(responseFactory));

        var sender = new EmbeddedWebhookSender(
            db,
            new StubHttpClientFactory(httpClient),
            dataProtection,
            new PassthroughWebhookUrlSecurityValidator());

        return new WebhookSenderFixture(sender, delivery.Id);
    }

    private static ImportProviderInvoiceFormDto InvoiceRequest(
        string invoiceNumber,
        IFormFile file)
    {
        var now = DateTime.UtcNow;

        return new ImportProviderInvoiceFormDto
        {
            ProviderCode = ProviderCode.Blaaiz,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = now,
            PeriodStart = now.AddDays(-7),
            PeriodEnd = now,
            CurrencyCode = "CAD",
            File = file
        };
    }

    private static IFormFile SizedFile(
        string fileName,
        long length) =>
        new FormFile(
            Stream.Null,
            0,
            length,
            "File",
            fileName);

    private static IFormFile TextFile(
        string fileName,
        string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(
            stream,
            0,
            bytes.Length,
            "File",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private sealed record WebhookSenderFixture(
        EmbeddedWebhookSender Sender,
        Guid DeliveryId);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(_factory(request));
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }

    private sealed class PassthroughWebhookUrlSecurityValidator
        : IEmbeddedWebhookUrlSecurityValidator
    {
        public Task<string> ValidateAsync(
            string value,
            CancellationToken ct = default) =>
            Task.FromResult(value);
    }
}
