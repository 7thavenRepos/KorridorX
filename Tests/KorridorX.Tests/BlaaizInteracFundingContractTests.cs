using System.Net;
using System.Text;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Providers;
using Xunit;

namespace KorridorX.Tests;

// Every test uses a local HttpMessageHandler fixture; no provider host can be contacted.
public sealed class BlaaizInteracFundingContractTests
{
    private const string RegistrationId = "01994c21-a4d0-7000-8000-123456789abc";

    [Theory]
    [InlineData("PENDING")]
    [InlineData("PROCESSING")]
    [InlineData("ACTIVE")]
    [InlineData("BLOCKED")]
    public async Task Registration_status_is_read_from_the_provider_without_creating_or_activating_anything(string status)
    {
        var body = RegistrationJson(status);
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var result = await client.GetInteracAutoDepositEmailAsync(" " + RegistrationId + " ");

        Assert.NotNull(result.Data.Data);
        Assert.Equal(RegistrationId, result.Data.Data.Id);
        Assert.Equal("receive@example.test", result.Data.Data.Email);
        Assert.Equal(status, result.Data.Data.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-09-16T10:00:00Z"), result.Data.Data.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-09-17T11:00:00Z"), result.Data.Data.UpdatedAt);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/external/interac-auto-deposit-email/" + RegistrationId, handler.PathAndQuery);
        Assert.Null(handler.RequestBody);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Equal(0, audit.FailCalls);
    }

    [Fact]
    public async Task Registration_listing_retains_pagination_and_encodes_filter_values()
    {
        const string body = """
            {"data":[{"id":"01994c21-a4d0-7000-8000-123456789abc","email":"receive+cad@example.test","status":"ACTIVE"}],
             "links":{"first":"https://provider.example.test?page=1","last":"https://provider.example.test?page=3","prev":"https://provider.example.test?page=1","next":"https://provider.example.test?page=3"},
             "meta":{"current_page":2,"last_page":3,"per_page":10,"total":25}}
            """;
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var client = new BlaaizApiClient(http, new TokenService(), new RecordingAudit());

        var result = await client.ListInteracAutoDepositEmailsAsync(" active ", " receive+cad@example.test ", 2);

        Assert.Equal("receive+cad@example.test", Assert.Single(result.Data.Data).Email);
        Assert.Equal(2, result.Data.Meta!.CurrentPage);
        Assert.Equal(3, result.Data.Meta.LastPage);
        Assert.Equal(10, result.Data.Meta.PerPage);
        Assert.Equal(25, result.Data.Meta.Total);
        Assert.Equal("https://provider.example.test?page=3", result.Data.Links!.Next);
        Assert.Equal("/api/external/interac-auto-deposit-email?page=2&status=ACTIVE&search_term=receive%2Bcad%40example.test", handler.PathAndQuery);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Null(handler.RequestBody);
        Assert.Equal(1, handler.Calls); // Never follows arbitrary links returned by the provider.
    }

    [Fact]
    public async Task Empty_registration_list_and_absent_registration_data_do_not_fabricate_an_active_route()
    {
        using var listHandler = new ResponseHandler("""{"data":[],"meta":{"current_page":1,"last_page":1,"per_page":10,"total":0}}""");
        using var listHttp = CreateHttp(listHandler);
        var listClient = new BlaaizApiClient(listHttp, new TokenService(), new RecordingAudit());
        var result = await listClient.ListInteracAutoDepositEmailsAsync();
        Assert.Empty(result.Data.Data);
        Assert.Equal(0, result.Data.Meta!.Total);

        using var singleHandler = new ResponseHandler("""{"data":null}""");
        using var singleHttp = CreateHttp(singleHandler);
        var singleClient = new BlaaizApiClient(singleHttp, new TokenService(), new RecordingAudit());
        var missing = await singleClient.GetInteracAutoDepositEmailAsync(RegistrationId);
        Assert.Null(missing.Data.Data);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    public async Task Registration_read_errors_keep_provider_HTTP_status_and_audit_evidence(int status)
    {
        const string body = """{"message":"Provider registration cannot be read."}""";
        using var handler = new ResponseHandler(body, (HttpStatusCode)status);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => client.GetInteracAutoDepositEmailAsync(RegistrationId));

        Assert.Equal(status, error.ProviderStatusCode);
        Assert.Equal(audit.Id, error.RequestLogId);
        Assert.Equal(body, error.ProviderResponse);
        Assert.Equal(0, audit.CompleteCalls);
        Assert.Equal(1, audit.FailCalls);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Invalid_registration_filters_fail_before_authentication_or_HTTP()
    {
        using var handler = new ResponseHandler("{}");
        using var http = CreateHttp(handler);
        var tokens = new TokenService();
        var client = new BlaaizApiClient(http, tokens, new RecordingAudit());

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetInteracAutoDepositEmailAsync("receive@example.test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetInteracAutoDepositEmailAsync(Guid.Empty.ToString()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListInteracAutoDepositEmailsAsync("READY"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListInteracAutoDepositEmailsAsync(page: 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListInteracAutoDepositEmailsAsync(searchTerm: new string('x', 256)));

        Assert.Equal(0, tokens.Calls);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Confirmed_collection_keeps_sender_receiver_provider_wallet_and_net_amount_distinct()
    {
        const string body = """
            {"message":"Transaction retrieved successfully","data":{
              "id":"txn-cad-001","business_id":"provider-business-001","business_customer_id":null,
              "business_wallet_id":"wallet-cad-001","status":"SUCCESSFUL","type":"collection",
              "reference":"CAD-REF-001","external_reference":"INTERAC-001","merchant_reference":null,
              "currency":"CAD","amount":100.25,"amount_without_fee":98.25,"fee":2.00,
              "date":"2026-10-18T12:00:00Z","refund_status":null,
              "payee_collection_email":"receive@example.test",
              "source_information":{"collection_email":"sender@example.test","collection_name":"Example Sender"},
              "recipient":null}}
            """;
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var result = await client.GetTransactionAsync("txn-cad-001");
        var transaction = result.Data.Data;

        Assert.Equal("txn-cad-001", transaction.Id);
        Assert.Equal("provider-business-001", transaction.BusinessId);
        Assert.Null(transaction.BusinessCustomerId);
        Assert.Equal("wallet-cad-001", transaction.BusinessWalletId);
        Assert.Equal("SUCCESSFUL", transaction.Status);
        Assert.Equal("collection", transaction.Type);
        Assert.Equal("CAD", transaction.Currency);
        Assert.Equal("CAD-REF-001", transaction.Reference);
        Assert.Equal("INTERAC-001", transaction.ExternalReference);
        Assert.Null(transaction.MerchantReference);
        Assert.Equal("receive@example.test", transaction.PayeeCollectionEmail);
        Assert.Equal("sender@example.test", transaction.SourceInformation!.CollectionEmail);
        Assert.Equal("Example Sender", transaction.SourceInformation.CollectionName);
        Assert.Equal(100.25m, transaction.Amount);
        Assert.Equal(98.25m, transaction.AmountWithoutFee);
        Assert.Equal(2m, transaction.Fee);
        Assert.Null(transaction.RefundStatus);
        Assert.Equal("/api/external/transaction/txn-cad-001", handler.PathAndQuery);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Null(handler.RequestBody);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(body, result.RawResponseJson);
        Assert.Equal(audit.Id, result.RequestLogId);
    }

    [Theory]
    [InlineData("FAILED")]
    [InlineData("EXPIRED")]
    public async Task Non_successful_transaction_does_not_infer_sender_or_destination_from_other_fields(string status)
    {
        var body = "{\"data\":{\"id\":\"txn-cad-002\",\"status\":\"" + status + "\",\"type\":\"collection\",\"currency\":\"CAD\",\"amount\":100,\"amount_without_fee\":98,\"fee\":2,\"source_information\":{\"collection_email\":null,\"collection_name\":null},\"payee_collection_email\":null}}";
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var client = new BlaaizApiClient(http, new TokenService(), new RecordingAudit());

        var transaction = (await client.GetTransactionAsync("txn-cad-002")).Data.Data;

        Assert.Equal(status, transaction.Status);
        Assert.Null(transaction.PayeeCollectionEmail);
        Assert.Null(transaction.SourceInformation!.CollectionEmail);
        Assert.Null(transaction.BusinessWalletId);
    }

    [Fact]
    public async Task Malformed_source_email_is_rejected_with_the_original_success_HTTP_status()
    {
        const string body = """{"data":{"id":"txn-cad-003","source_information":{"collection_email":{"email":"sender@example.test"}}}}""";
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => client.GetTransactionAsync("txn-cad-003"));

        Assert.Equal(200, error.ProviderStatusCode);
        Assert.IsType<JsonException>(error.InnerException);
        Assert.Contains("source_information.collection_email", error.Message);
        Assert.Equal(1, audit.FailCalls);
        Assert.Equal(0, audit.CompleteCalls);
    }

    private static string RegistrationJson(string status) =>
        "{\"data\":{\"id\":\"" + RegistrationId + "\",\"email\":\"receive@example.test\",\"status\":\"" + status + "\",\"created_at\":\"2026-09-16T10:00:00.000000Z\",\"updated_at\":\"2026-09-17T11:00:00.000000Z\"}}";

    private static HttpClient CreateHttp(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };

    private sealed class TokenService : IBlaaizTokenService
    {
        public int Calls { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult("test-token");
        }

        public Task<string> GetInteracAutoDepositReadAccessTokenAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult("test-token");
        }
    }

    private sealed class ResponseHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? PathAndQuery { get; private set; }
        public string? Authorization { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Method = request.Method;
            PathAndQuery = request.RequestUri!.PathAndQuery;
            Authorization = request.Headers.Authorization?.ToString();
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class RecordingAudit : IProviderRequestAuditService
    {
        public Guid Id { get; } = Guid.NewGuid();
        public int CompleteCalls { get; private set; }
        public int FailCalls { get; private set; }

        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default) => Task.FromResult(Id);

        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson,
            long durationMs, CancellationToken ct = default)
        {
            CompleteCalls++;
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson,
            string errorMessage, long durationMs, CancellationToken ct = default)
        {
            FailCalls++;
            return Task.CompletedTask;
        }
    }
}
