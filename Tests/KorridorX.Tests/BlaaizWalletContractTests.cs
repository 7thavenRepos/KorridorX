using System.Net;
using System.Text;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Providers;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizWalletContractTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("0", "0")]
    [InlineData("18446744073709551616001", "18446744073709551616001")]
    [InlineData("\"0007\"", "0007")]
    [InlineData("\"currency-cad\"", "currency-cad")]
    public async Task Discovery_accepts_numeric_and_string_currency_ids_without_changing_values(
        string currencyIdJson, string expectedId)
    {
        var body = "[" + WalletJson(currencyIdJson) + "]";
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var result = await client.ListWalletsAsync();

        var wallet = Assert.Single(result.Data);
        Assert.Equal(expectedId, wallet.CurrencyId);
        Assert.Equal("wallet-cad", wallet.Id);
        Assert.Equal("CAD", wallet.Currency);
        Assert.Equal(1234.5678m, wallet.Amount);
        Assert.True(wallet.IsActive);
        Assert.Equal("/api/external/wallet", handler.Path);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Equal(0, audit.FailCalls);
        Assert.Equal(200, audit.StatusCode);
        Assert.Equal(body, audit.Response);
    }

    [Theory]
    [InlineData("7", "7")]
    [InlineData("\"currency-cad\"", "currency-cad")]
    public async Task Wallet_verification_accepts_both_currency_id_formats(
        string currencyIdJson, string expectedId)
    {
        using var handler = new ResponseHandler(WalletJson(currencyIdJson));
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var result = await client.GetWalletAsync("wallet-cad");

        Assert.Equal(expectedId, result.Data.CurrencyId);
        Assert.Equal("/api/external/wallet/wallet-cad", handler.Path);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Equal(0, audit.FailCalls);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("1e2")]
    public async Task Invalid_currency_id_is_rejected_and_retains_the_received_HTTP_status(
        string currencyIdJson)
    {
        var body = "[" + WalletJson(currencyIdJson) + "]";
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var exception = await Assert.ThrowsAsync<ProviderIntegrationException>(() => client.ListWalletsAsync());

        Assert.Equal(200, exception.ProviderStatusCode);
        Assert.Equal(body, exception.ProviderResponse);
        Assert.Equal(audit.Id, exception.RequestLogId);
        Assert.IsType<JsonException>(exception.InnerException);
        Assert.Contains("$[0].currency_id", exception.Message);
        Assert.Contains("incompatible JSON", exception.Message);
        Assert.Equal(0, audit.CompleteCalls);
        Assert.Equal(1, audit.FailCalls);
        Assert.Equal(200, audit.StatusCode);
        Assert.Equal(body, audit.Response);
        Assert.Contains("currency_id", audit.Error);
    }

    [Fact]
    public async Task Transport_failure_still_has_no_provider_HTTP_status()
    {
        using var handler = new ResponseHandler("", failTransport: true);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);

        var exception = await Assert.ThrowsAsync<ProviderIntegrationException>(() => client.ListWalletsAsync());

        Assert.Null(exception.ProviderStatusCode);
        Assert.Null(exception.ProviderResponse);
        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Equal("Unable to communicate with Blaaiz.", exception.Message);
        Assert.Equal(1, audit.FailCalls);
        Assert.Null(audit.StatusCode);
        Assert.Null(audit.Response);
    }

    private static string WalletJson(string currencyIdJson) =>
        "{\"id\":\"wallet-cad\",\"business_id\":\"business-test\",\"currency\":\"CAD\"," +
        "\"currency_id\":" + currencyIdJson + ",\"amount\":1234.5678,\"is_active\":true}";

    private static HttpClient CreateHttp(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };

    private sealed class TokenService : IBlaaizTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) =>
            Task.FromResult("test-token");
    }

    private sealed class ResponseHandler(string body, bool failTransport = false) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (failTransport) throw new HttpRequestException("Test connection failure.");
            Path = request.RequestUri!.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class RecordingAudit : IProviderRequestAuditService
    {
        public Guid Id { get; } = Guid.NewGuid();
        public int CompleteCalls { get; private set; }
        public int FailCalls { get; private set; }
        public int? StatusCode { get; private set; }
        public string? Response { get; private set; }
        public string? Error { get; private set; }

        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default) =>
            Task.FromResult(Id);

        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson,
            long durationMs, CancellationToken ct = default)
        {
            CompleteCalls++;
            StatusCode = responseStatusCode;
            Response = responseBodyJson;
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson,
            string errorMessage, long durationMs, CancellationToken ct = default)
        {
            FailCalls++;
            StatusCode = responseStatusCode;
            Response = responseBodyJson;
            Error = errorMessage;
            return Task.CompletedTask;
        }
    }
}
