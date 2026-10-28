using System.Net;
using System.Text;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Providers;

namespace KorridorX.Tests;

public sealed class BlaaizErrorDetailsTests
{
    [Fact]
    public async Task Nested_provider_error_keeps_HTTP_status_response_and_transfer_audit_link()
    {
        using var handler = new ErrorHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        var audit = new RequestAudit();
        var client = new BlaaizApiClient(http, new Token(), audit);
        var transferId = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() =>
            client.InitiatePayoutAsync(new BlaaizPayoutRequest(), transferId, Guid.NewGuid()));
        Assert.Equal(422, error.ProviderStatusCode);
        Assert.Equal("INVALID_RECIPIENT", error.ProviderErrorCode);
        Assert.Equal("req-header", error.ProviderRequestReference);
        Assert.Equal(audit.Id, error.RequestLogId);
        Assert.Equal(transferId, audit.TransferId);
        Assert.Equal(422, audit.Status);
        Assert.Contains("INVALID_RECIPIENT", audit.Message);
        Assert.Contains("INVALID_RECIPIENT", audit.Body);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Authentication_error_is_linked_to_intended_transfer_without_a_payout_POST()
    {
        using var handler = new ErrorHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://blaaiz.example.test") };
        var audit = new RequestAudit();
        var original = new ProviderIntegrationException("Blaaiz authorization denied.", 401,
            requestLogId: Guid.NewGuid(), providerErrorCode: "INVALID_CLIENT");
        var client = new BlaaizApiClient(http, new Token(original), audit);
        var transferId = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<ProviderIntegrationException>(() =>
            client.InitiatePayoutAsync(new BlaaizPayoutRequest(), transferId, Guid.NewGuid()));
        Assert.Equal(0, handler.Calls);
        Assert.Equal(transferId, audit.TransferId);
        Assert.Equal(audit.Id, error.RequestLogId);
        Assert.Equal(401, audit.Status);
        Assert.Equal("INVALID_CLIENT", error.ProviderErrorCode);
        Assert.Null(audit.Body);
    }

    [Fact]
    public void Nested_error_preserves_provider_message_code_reference_and_validation_details()
    {
        var result = BlaaizErrorDetails.Read("""
            {"error":{"code":"INVALID_RECIPIENT","message":"Recipient details are invalid",
              "request_id":"req-abc","errors":{"routing_number":["Invalid routing format"]}}}
            """, "HTTP 422");
        Assert.Equal("INVALID_RECIPIENT", result.Code);
        Assert.Equal("req-abc", result.ProviderRequestReference);
        Assert.Contains("Recipient details are invalid", result.Message);
        Assert.Contains("routing_number: Invalid routing format", result.Message);
        Assert.Contains("[INVALID_RECIPIENT]", result.Format());
    }

    [Theory]
    [InlineData("""{"error":["unexpected"]}""")]
    [InlineData("""["not an error object"]""")]
    [InlineData("""{"message":{"unexpected":true}}""")]
    [InlineData("<html>upstream unavailable</html>")]
    public void Unexpected_body_shapes_do_not_hide_the_original_HTTP_fallback(string body)
    {
        var result = BlaaizErrorDetails.Read(body, "Blaaiz request failed with HTTP 502.");
        Assert.Equal("Blaaiz request failed with HTTP 502.", result.Message);
    }

    [Fact]
    public void Diagnostic_text_redacts_credentials_email_bank_numbers_and_IBAN()
    {
        var result = BlaaizErrorDetails.Safe(
            "client_secret=secret-value access_token=opaque-token Bearer other-token " +
            "person@example.test account 0123456789 IBAN GB82WEST12345698765432 explicit-secret",
            "explicit-secret");
        Assert.DoesNotContain("secret-value", result);
        Assert.DoesNotContain("opaque-token", result);
        Assert.DoesNotContain("other-token", result);
        Assert.DoesNotContain("person@example.test", result);
        Assert.DoesNotContain("0123456789", result);
        Assert.DoesNotContain("GB82WEST", result);
        Assert.DoesNotContain("explicit-secret", result);
    }

    private sealed class Token(ProviderIntegrationException? failure = null) : IBlaaizTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) =>
            failure is null ? Task.FromResult("test-token") : Task.FromException<string>(failure);
    }

    private sealed class ErrorHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent(
                    """{"error":{"code":"INVALID_RECIPIENT","message":"Recipient details rejected"}}""",
                    Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("X-Request-ID", "req-header");
            return Task.FromResult(response);
        }
    }

    private sealed class RequestAudit : IProviderRequestAuditService
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid? TransferId { get; private set; }
        public int? Status { get; private set; }
        public string? Message { get; private set; }
        public string? Body { get; private set; }
        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default)
        {
            TransferId = relatedTransferId;
            return Task.FromResult(Id);
        }
        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson,
            long durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson,
            string errorMessage, long durationMs, CancellationToken ct = default)
        {
            Status = responseStatusCode;
            Message = errorMessage;
            Body = responseBodyJson;
            return Task.CompletedTask;
        }
    }
}
