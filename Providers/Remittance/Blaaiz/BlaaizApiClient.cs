using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Providers;

namespace KorridorX.Providers.Remittance.Blaaiz;

public class BlaaizApiClient : IBlaaizApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IBlaaizTokenService _tokenService;
    private readonly IProviderRequestAuditService _auditService;

    public BlaaizApiClient(
        HttpClient httpClient,
        IBlaaizTokenService tokenService,
        IProviderRequestAuditService auditService)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    public Task<BlaaizApiResult<BlaaizCustomerEnvelope>> CreateCustomerAsync(
        BlaaizCreateCustomerRequest request,
        Guid customerProfileId,
        CancellationToken ct = default)
    {
        var auditBody = JsonSerializer.Serialize(new
        {
            request.Type,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Country,
            request.IdType,
            id_number = MaskSensitive(request.IdNumber),
            request.Phone,
            request.DateOfBirth,
            request.Street,
            request.City,
            request.State,
            request.ZipCode,
            request.IdExpiryDate,
            request.IdIssueDate,
            customerProfileId
        });

        return SendAsync<BlaaizCreateCustomerRequest, BlaaizCustomerEnvelope>(
            HttpMethod.Post,
            "/api/external/customer",
            request,
            auditBody,
            null,
            null,
            null,
            RedactCustomerResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizCustomerEnvelope>> GetCustomerAsync(
        string providerCustomerId,
        Guid customerProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}";

        return SendAsync<object, BlaaizCustomerEnvelope>(
            HttpMethod.Get,
            endpoint,
            null,
            JsonSerializer.Serialize(new { customerProfileId, providerCustomerId }),
            null,
            null,
            null,
            RedactCustomerResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizCardCollectionResponse>> InitiateCardCollectionAsync(
        BlaaizCardCollectionRequest request,
        Guid transferId,
        Guid collectionId,
        CancellationToken ct = default)
    {
        var auditBody = JsonSerializer.Serialize(new
        {
            method = request.Method,
            request.Amount,
            wallet_id = request.WalletId,
            customer_id = request.CustomerId,
            card_holder_name = request.CardHolderName,
            card_number = MaskCardNumber(request.CardNumber),
            expiry = request.Expiry,
            cvc = "***REDACTED***",
            request.Phone,
            redirect_url = request.RedirectUrl
        });

        return SendAsync<BlaaizCardCollectionRequest, BlaaizCardCollectionResponse>(
            HttpMethod.Post,
            "/api/external/collection",
            request,
            auditBody,
            transferId,
            collectionId,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizInteracMoneyResponse>> InitiateInteracMoneyRequestAsync(
        BlaaizInteracMoneyRequest request,
        Guid transferId,
        Guid collectionId,
        CancellationToken ct = default)
    {
        var auditBody = JsonSerializer.Serialize(request, SerializerOptions);

        return SendAsync<BlaaizInteracMoneyRequest, BlaaizInteracMoneyResponse>(
            HttpMethod.Post,
            "/api/external/collection/interac-money-request",
            request,
            auditBody,
            transferId,
            collectionId,
            null,
            null,
            ct);
    }

    private async Task<BlaaizApiResult<TResponse>> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string endpoint,
        TRequest? requestBody,
        string? auditRequestBody,
        Guid? relatedTransferId,
        Guid? relatedCollectionId,
        Guid? relatedPayoutId,
        Func<string, string>? responseSanitizer,
        CancellationToken ct)
    {
        var token = await _tokenService.GetAccessTokenAsync(ct);

        var requestLogId = await _auditService.StartAsync(
            ProviderCode.Blaaiz,
            endpoint,
            method.Method,
            JsonSerializer.Serialize(new
            {
                authorization = "Bearer ***REDACTED***",
                contentType = requestBody is null ? null : "application/json"
            }),
            auditRequestBody,
            relatedTransferId,
            relatedCollectionId,
            relatedPayoutId,
            ct);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(method, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (requestBody is not null)
            {
                var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request, ct);
            var rawResponse = await response.Content.ReadAsStringAsync(ct);
            var auditResponse = responseSanitizer?.Invoke(rawResponse) ?? rawResponse;
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractErrorMessage(
                    rawResponse,
                    $"Blaaiz request failed with HTTP {(int)response.StatusCode}.");

                await _auditService.FailAsync(
                    requestLogId,
                    (int)response.StatusCode,
                    auditResponse,
                    message,
                    stopwatch.ElapsedMilliseconds,
                    ct);

                throw new ProviderIntegrationException(
                    message,
                    (int)response.StatusCode,
                    auditResponse,
                    requestLogId);
            }

            var parsed = JsonSerializer.Deserialize<TResponse>(rawResponse, SerializerOptions);
            if (parsed is null)
            {
                const string message = "Blaaiz returned an invalid JSON response.";

                await _auditService.FailAsync(
                    requestLogId,
                    (int)response.StatusCode,
                    auditResponse,
                    message,
                    stopwatch.ElapsedMilliseconds,
                    ct);

                throw new ProviderIntegrationException(
                    message,
                    (int)response.StatusCode,
                    auditResponse,
                    requestLogId);
            }

            await _auditService.CompleteAsync(
                requestLogId,
                (int)response.StatusCode,
                auditResponse,
                stopwatch.ElapsedMilliseconds,
                ct);

            return new BlaaizApiResult<TResponse>(parsed, auditResponse, requestLogId);
        }
        catch (ProviderIntegrationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await _auditService.FailAsync(
                requestLogId,
                null,
                null,
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                ct);

            throw new ProviderIntegrationException(
                "Unable to communicate with Blaaiz.",
                requestLogId: requestLogId,
                innerException: ex);
        }
    }

    private static string ExtractErrorMessage(string rawResponse, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var root = document.RootElement;

            if (root.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? fallback;
            }

            if (root.TryGetProperty("error_description", out var description))
            {
                return description.GetString() ?? fallback;
            }

            if (root.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }

    private static string RedactCustomerResponse(string rawResponse)
    {
        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var root = document.RootElement;

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return rawResponse;
            }

            var sanitized = new Dictionary<string, object?>();
            foreach (var property in data.EnumerateObject())
            {
                sanitized[property.Name] = property.NameEquals("id_number")
                    ? "***REDACTED***"
                    : JsonSerializer.Deserialize<object?>(property.Value.GetRawText(), SerializerOptions);
            }

            var message = root.TryGetProperty("message", out var messageProperty)
                ? messageProperty.GetString()
                : null;

            return JsonSerializer.Serialize(new { message, data = sanitized }, SerializerOptions);
        }
        catch (JsonException)
        {
            return rawResponse;
        }
    }

    private static string MaskSensitive(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 4)
        {
            return "****";
        }

        return $"***{value[^4..]}";
    }

    private static string MaskCardNumber(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? $"************{digits[^4..]}" : "****";
    }
}
