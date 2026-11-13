using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Providers;

namespace KorridorX.Tests;

/// <summary>No real provider calls: all requests are captured by the local handler.</summary>
public sealed class BlaaizPrincipalIdentificationContractTests
{
    [Theory]
    [InlineData("{\"data\":{\"label\":\"Bank Verification Number\",\"country\":\"NG\",\"customer_type\":\"individual\"}}")]
    [InlineData("{\"label\":\"Bank Verification Number\",\"country\":\"NG\",\"customer_type\":\"individual\"}")]
    public async Task Identification_label_is_looked_up_for_existing_individual_customer_in_both_provider_formats(string response)
    {
        using var handler = new RecordingHandler(HttpStatusCode.OK, response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        var client = new BlaaizApiClient(http, new FixedToken(), new RecordingAudit());

        var result = await client.GetIndividualIdentificationTypeAsync("individual-123", Guid.NewGuid());

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/external/virtual-bank-account/identification-type?customer_id=individual-123", handler.PathAndQuery);
        Assert.Equal("individual", result.Data.ResolvedIdentification.CustomerType);
        Assert.Equal("NG", result.Data.ResolvedIdentification.Country);
        Assert.Equal("Bank Verification Number", result.Data.ResolvedIdentification.Label);
    }

    [Fact]
    public async Task Flat_provider_business_type_is_not_silently_coerced_to_individual()
    {
        const string response = """{"label":"Tax Identification Number","country":"NG","customer_type":"business"}""";
        using var handler = new RecordingHandler(HttpStatusCode.OK, response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        var client = new BlaaizApiClient(http, new FixedToken(), new RecordingAudit());

        var result = await client.GetIndividualIdentificationTypeAsync("individual-123", Guid.NewGuid());

        Assert.Equal("business", result.Data.ResolvedIdentification.CustomerType);
        Assert.Equal("NG", result.Data.ResolvedIdentification.Country);
        AssertIdentificationRejected(result.Data.ResolvedIdentification);
    }

    [Fact]
    public async Task Missing_provider_identification_fields_are_not_filled_by_defaults()
    {
        using var handler = new RecordingHandler(HttpStatusCode.OK, """{"message":"ok"}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        var client = new BlaaizApiClient(http, new FixedToken(), new RecordingAudit());

        var result = await client.GetIndividualIdentificationTypeAsync("individual-123", Guid.NewGuid());

        Assert.Equal("", result.Data.ResolvedIdentification.Label);
        Assert.Equal("", result.Data.ResolvedIdentification.Country);
        Assert.Equal("", result.Data.ResolvedIdentification.CustomerType);
        AssertIdentificationRejected(result.Data.ResolvedIdentification);
    }

    [Fact]
    public async Task Only_tin_is_sent_on_wire_and_it_is_redacted_in_all_audit_fields()
    {
        const string sensitive = "11122233344";
        const string response = """{"message":"updated","data":{"id":"individual-123","type":"individual","verification_status":"VERIFIED","tin":"11122233344"}}""";
        using var handler = new RecordingHandler(HttpStatusCode.OK, response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new FixedToken(), audit);

        var result = await client.UpdateIndividualCustomerTinAsync("individual-123", sensitive, Guid.NewGuid());

        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.Equal("/api/external/customer/individual-123", handler.PathAndQuery);
        using var wire = JsonDocument.Parse(handler.WireBody!);
        Assert.Equal(sensitive, wire.RootElement.GetProperty("tin").GetString());
        Assert.Single(wire.RootElement.EnumerateObject());
        Assert.DoesNotContain(sensitive, audit.RequestBody ?? "");
        Assert.DoesNotContain(sensitive, audit.ResponseBody ?? "");
        Assert.Equal(1, audit.Completed);
        Assert.Equal("VERIFIED", result.Data.Data.VerificationStatus);
        Assert.DoesNotContain(sensitive, result.RawResponseJson);
    }

    [Fact]
    public async Task Provider_rejection_cannot_echo_sensitive_identification_to_client_or_audit()
    {
        const string sensitive = "ABCD1234XYZ";
        const string response = """{"message":"Invalid tin ABCD1234XYZ","code":"ABCD1234XYZ","request_id":"ABCD1234XYZ","errors":{"tin":["ABCD1234XYZ rejected"]}}""";
        using var handler = new RecordingHandler(HttpStatusCode.BadRequest, response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new FixedToken(), audit);

        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() =>
            client.UpdateIndividualCustomerTinAsync("individual-123", sensitive, Guid.NewGuid()));

        Assert.DoesNotContain(sensitive, error.Message);
        Assert.DoesNotContain(sensitive, error.ProviderResponse ?? "");
        Assert.Null(error.ValidationErrors);
        Assert.Null(error.ProviderErrorCode);
        Assert.Null(error.ProviderRequestReference);
        Assert.DoesNotContain(sensitive, audit.RequestBody ?? "");
        Assert.DoesNotContain(sensitive, audit.ResponseBody ?? "");
        Assert.DoesNotContain(sensitive, audit.ErrorMessage ?? "");
    }

    private static void AssertIdentificationRejected(BlaaizIdentificationTypeData value)
    {
        var validator = typeof(WalletDepositAccountService).GetMethod(
            "VerifiedIdentificationType", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The identification guard could not be located.");
        var error = Assert.Throws<TargetInvocationException>(() =>
            validator.Invoke(null, new object[] { value }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private sealed class FixedToken : IBlaaizTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("unit-test-token");
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? PathAndQuery { get; private set; }
        public string? WireBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Method = request.Method;
            PathAndQuery = request.RequestUri?.PathAndQuery;
            WireBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class RecordingAudit : IProviderRequestAuditService
    {
        public string? RequestBody { get; private set; }
        public string? ResponseBody { get; private set; }
        public string? ErrorMessage { get; private set; }
        public int Completed { get; private set; }

        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default)
        {
            RequestBody = requestBodyJson;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson,
            long durationMs, CancellationToken ct = default)
        {
            ResponseBody = responseBodyJson;
            Completed++;
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson,
            string errorMessage, long durationMs, CancellationToken ct = default)
        {
            ResponseBody = responseBodyJson;
            ErrorMessage = errorMessage;
            return Task.CompletedTask;
        }
    }
}
