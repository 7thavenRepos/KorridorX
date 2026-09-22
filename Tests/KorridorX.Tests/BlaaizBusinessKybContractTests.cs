using System.Net;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Providers;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizBusinessKybContractTests
{
    [Theory]
    [InlineData("FULL")]
    [InlineData("MINIMAL")]
    public async Task Update_omits_creation_only_scope_but_keeps_owner_fields_and_does_not_mutate_input(string scope)
    {
        using var handler = new ResponseHandler("""{"data":{"id":"business-provider-id","owners":[],"documents":[]}}""");
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var client = new BlaaizApiClient(http, new TokenService(), audit);
        var input = new BlaaizCreateCustomerRequest
        {
            Type = "business", KybScope = scope, BusinessName = "Test company", Country = "CA",
            Email = "company@example.test", Tin = "sensitive-tax-number",
            Owners = [new BlaaizBusinessOwnerRequest
            {
                FirstName = "Test", LastName = "Owner", Email = "owner@example.test",
                OwnershipPercentage = 100m, IdDocumentNumber = "sensitive-owner-id"
            }]
        };

        await client.UpdateBusinessCustomerAsync("business-provider-id", input, Guid.NewGuid());

        using var update = JsonDocument.Parse(handler.Body!);
        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.Equal("/api/external/customer/business-provider-id", handler.Path);
        Assert.False(update.RootElement.TryGetProperty("kyb_scope", out _));
        Assert.Equal("Test company", update.RootElement.GetProperty("business_name").GetString());
        Assert.Equal(100m, update.RootElement.GetProperty("owners")[0].GetProperty("ownership_percentage").GetDecimal());
        Assert.Equal("sensitive-owner-id", update.RootElement.GetProperty("owners")[0].GetProperty("id_document_number").GetString());
        Assert.DoesNotContain("sensitive-owner-id", audit.Request);
        Assert.DoesNotContain("sensitive-tax-number", audit.Request);
        using var updateAudit = JsonDocument.Parse(audit.Request!);
        Assert.False(updateAudit.RootElement.TryGetProperty("KybScope", out _));
        Assert.Equal(scope, input.KybScope);

        await client.CreateBusinessCustomerAsync(input, Guid.NewGuid());
        using var create = JsonDocument.Parse(handler.Body!);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(scope, create.RootElement.GetProperty("kyb_scope").GetString());
        Assert.Equal(scope, input.KybScope);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Document_and_owner_uploads_return_documented_values_and_keep_signed_URL_out_of_audit(bool owner)
    {
        const string body = """{"message":"Upload URL generated successfully","data":{"file_id":"file-123","url":"https://upload.example.test/object?X-Amz-Signature=private-signature","headers":{"x-amz-acl":"private"}}}""";
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var provider = CreateProvider(http, audit);

        var result = await Upload(provider, owner);

        Assert.Equal("file-123", result.ProviderFileId);
        Assert.Equal("https://upload.example.test/object?X-Amz-Signature=private-signature", result.UploadUrl);
        Assert.Equal("private", result.UploadHeaders["x-amz-acl"]);
        Assert.EndsWith(owner ? "/owner/owner-id/file/presigned-url" : "/document/presigned-url", handler.Path);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Equal(0, audit.FailCalls);
        Assert.DoesNotContain("private-signature", audit.Response);
        Assert.DoesNotContain("private-signature", result.RawResponseJson);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Live_root_and_documented_nested_responses_normalize_array_headers_without_changing_signed_URL(bool owner, bool nested)
    {
        const string url = "https://upload.example.test/object?X-Amz-Signature=private-signature&X-Amz-Credential=a%2Fb";
        var target = new { file_id = "file-123", url, headers = new Dictionary<string, object>
        {
            ["Host"] = new[] { "upload.example.test" },
            ["Content-Type"] = new[] { "application/pdf" },
            ["x-amz-acl"] = "private",
            ["x-amz-meta-tags"] = new[] { "first", "second" }
        } };
        var body = nested ? JsonSerializer.Serialize(new { data = target }) : JsonSerializer.Serialize(target);
        using var handler = new ResponseHandler(body);
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var result = await Upload(CreateProvider(http, audit), owner);
        Assert.Equal("file-123", result.ProviderFileId);
        Assert.Equal(url, result.UploadUrl);
        Assert.False(result.UploadHeaders.ContainsKey("Host"));
        Assert.Equal("application/pdf", result.UploadHeaders["Content-Type"]);
        Assert.Equal("private", result.UploadHeaders["x-amz-acl"]);
        Assert.Equal("first,second", result.UploadHeaders["x-amz-meta-tags"]);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Equal(0, audit.FailCalls);
        Assert.DoesNotContain("private-signature", audit.Response);
        Assert.DoesNotContain("private-signature", result.RawResponseJson);
    }

    [Theory]
    [InlineData("{\"Host\":[\"upload.example.test\"]}")]
    [InlineData("{\"host\":\"upload.example.test\"}")]
    public async Task Observed_Host_only_response_returns_usable_target_with_no_script_set_headers(string headers)
    {
        using var handler = new ResponseHandler("{\"file_id\":\"file-123\",\"url\":\"https://upload.example.test/file\",\"headers\":"+headers+"}");
        using var http = CreateHttp(handler);
        var result = await Upload(CreateProvider(http, new RecordingAudit()), false);
        Assert.Equal("file-123", result.ProviderFileId);
        Assert.Equal("https://upload.example.test/file", result.UploadUrl);
        Assert.Empty(result.UploadHeaders);
    }

    [Theory]
    [InlineData("{\"Host\":[\"wrong.example.test\"]}")]
    [InlineData("{\"Host\":[\"upload.example.test\",\"wrong.example.test\"]}")]
    [InlineData("{\"x-amz-acl\":[]}")]
    [InlineData("{\"x-amz-acl\":[\"private\",4]}")]
    [InlineData("{\"x-amz-acl\":null}")]
    [InlineData("{\"x-amz-acl\":true}")]
    [InlineData("{\"x-amz-acl\":\"private\\r\\nInjected: yes\"}")]
    [InlineData("{\"x-amz-acl\":\"private\",\"X-Amz-Acl\":\"public\"}")]
    public async Task Invalid_header_contracts_fail_with_audited_provider_status(string headers)
    {
        using var handler = new ResponseHandler("{\"file_id\":\"file-123\",\"url\":\"https://upload.example.test/file\",\"headers\":"+headers+"}");
        using var http = CreateHttp(handler);
        var audit = new RecordingAudit();
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => Upload(CreateProvider(http, audit), false));
        Assert.Equal(200, error.ProviderStatusCode);
        Assert.Equal(audit.Id, error.RequestLogId);
        Assert.IsType<JsonException>(error.InnerException);
        Assert.Equal(1, audit.FailCalls);
        Assert.Equal(0, audit.CompleteCalls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"data\":{\"file_id\":\"\",\"url\":\"https://upload.example.test/file\"}}")]
    [InlineData("{\"data\":{\"file_id\":\"file-123\",\"url\":\"\"}}")]
    [InlineData("{\"data\":{\"file_id\":\"file-123\",\"url\":\"/relative\"}}")]
    [InlineData("{\"data\":{\"file_id\":\"file-123\",\"url\":\"http://upload.example.test/file\"}}")]
    [InlineData("{\"data\":{\"file_id\":\"file-123\",\"url\":\"https://upload.example.test/file\",\"headers\":null}}")]
    [InlineData("{\"file_id\":\"\",\"url\":\"https://upload.example.test/file\",\"headers\":{}}")]
    [InlineData("{\"file_id\":\"file-123\",\"url\":\"\",\"headers\":{}}")]
    [InlineData("{\"file_id\":\"file-123\",\"url\":\"https://upload.example.test/file\",\"headers\":null}")]
    [InlineData("{\"file_id\":\"file-123\",\"data\":{\"url\":\"https://upload.example.test/file\",\"headers\":{}}}")]
    [InlineData("{\"url\":\"https://wrong.example.test/file\",\"data\":{\"file_id\":\"file-123\",\"url\":\"https://upload.example.test/file\",\"headers\":{}}}")]
    public async Task Incomplete_upload_responses_fail_before_returning_a_success_result(string body)
    {
        foreach (var owner in new[] { false, true })
        {
            using var handler = new ResponseHandler(body);
            using var http = CreateHttp(handler);
            var audit = new RecordingAudit();
            var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() => Upload(CreateProvider(http, audit), owner));
            Assert.Equal(200, error.ProviderStatusCode);
            Assert.Equal(audit.Id, error.RequestLogId);
            Assert.IsType<JsonException>(error.InnerException);
            Assert.Equal(1, audit.FailCalls);
            Assert.Equal(0, audit.CompleteCalls);
            Assert.Equal(200, audit.StatusCode);
        }
    }

    private static HttpClient CreateHttp(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
    private static BlaaizRemittanceProvider CreateProvider(HttpClient http, RecordingAudit audit) =>
        new(new BlaaizApiClient(http, new TokenService(), audit), new TokenService(), Options.Create(new BlaaizOptions()));
    private static Task<RemittanceBusinessUploadUrlResult> Upload(BlaaizRemittanceProvider provider, bool owner) => owner
        ? provider.RequestBusinessOwnerUploadUrlAsync(new(Guid.NewGuid(), "customer-id", "owner-id", BusinessOwnerDocumentSide.Front))
        : provider.RequestBusinessDocumentUploadUrlAsync(new(Guid.NewGuid(), "customer-id"));

    private sealed class TokenService : IBlaaizTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("test-token");
    }

    private sealed class ResponseHandler(string body) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class RecordingAudit : IProviderRequestAuditService
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string? Request { get; private set; }
        public string? Response { get; private set; }
        public int? StatusCode { get; private set; }
        public int CompleteCalls { get; private set; }
        public int FailCalls { get; private set; }
        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default)
        {
            Request = requestBodyJson;
            return Task.FromResult(Id);
        }
        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson,
            long durationMs, CancellationToken ct = default)
        {
            CompleteCalls++; StatusCode = responseStatusCode; Response = responseBodyJson;
            return Task.CompletedTask;
        }
        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson,
            string errorMessage, long durationMs, CancellationToken ct = default)
        {
            FailCalls++; StatusCode = responseStatusCode; Response = responseBodyJson;
            return Task.CompletedTask;
        }
    }
}
