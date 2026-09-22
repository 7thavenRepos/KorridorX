using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public Task<BlaaizApiResult<BlaaizCryptoWalletListResponse>> ListCryptoWalletsAsync(
        CancellationToken ct = default)
    {
        return SendAsync<object, BlaaizCryptoWalletListResponse>(
            HttpMethod.Get,
            "/api/external/crypto/wallets",
            null,
            null,
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizCryptoPayoutResponse>> InitiateCryptoPayoutAsync(
        BlaaizCryptoPayoutRequest request,
        string idempotencyKey,
        Guid payoutId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new InvalidOperationException("Blaaiz crypto payout idempotency key is required.");

        var cleanKey = idempotencyKey.Trim();
        if (cleanKey.Length > 255)
            throw new InvalidOperationException("Blaaiz crypto payout idempotency key cannot exceed 255 characters.");

        var auditBody = JsonSerializer.Serialize(new
        {
            customer_id = request.CustomerId,
            wallet_id = request.WalletId,
            amount = request.Amount,
            address = MaskSensitive(request.Address),
            network = request.Network,
            token = request.Token
        }, SerializerOptions);

        return SendAsync<BlaaizCryptoPayoutRequest, BlaaizCryptoPayoutResponse>(
            HttpMethod.Post,
            "/api/external/crypto/payouts",
            request,
            auditBody,
            null,
            null,
            payoutId,
            null,
            ct,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Idempotency-Key"] = cleanKey
            });
    }

    public Task<BlaaizApiResult<BlaaizCryptoCollectionResponse>> InitiateCryptoCollectionAsync(
        BlaaizCryptoCollectionRequest request,
        Guid depositIntentId,
        CancellationToken ct = default)
    {
        var auditBody = JsonSerializer.Serialize(new
        {
            depositIntentId,
            amount = request.Amount,
            wallet_id = request.WalletId,
            network = request.Network,
            token = request.Token,
            customer_id = request.CustomerId
        }, SerializerOptions);

        return SendAsync<BlaaizCryptoCollectionRequest, BlaaizCryptoCollectionResponse>(
            HttpMethod.Post,
            "/api/external/collection/crypto",
            request,
            auditBody,
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<List<BlaaizWalletData>>> ListWalletsAsync(
        CancellationToken ct = default)
    {
        return SendAsync<object, List<BlaaizWalletData>>(
            HttpMethod.Get,
            "/api/external/wallet",
            null,
            null,
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizWalletData>> GetWalletAsync(
        string providerWalletId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerWalletId))
            throw new InvalidOperationException("Provider wallet ID is required.");

        var endpoint = $"/api/external/wallet/{Uri.EscapeDataString(providerWalletId.Trim())}";
        return SendAsync<object, BlaaizWalletData>(
            HttpMethod.Get,
            endpoint,
            null,
            JsonSerializer.Serialize(new { providerWalletId }),
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizSwapResponse>> SwapBusinessWalletsAsync(
        BlaaizSwapRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FromBusinessWalletId) ||
            string.IsNullOrWhiteSpace(request.ToBusinessWalletId))
            throw new InvalidOperationException("Both provider wallet IDs are required for a swap.");

        return SendAsync<BlaaizSwapRequest, BlaaizSwapResponse>(
            HttpMethod.Post,
            "/api/external/swap",
            request,
            JsonSerializer.Serialize(request, SerializerOptions),
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<List<BlaaizBankData>>> ListBanksAsync(
        string? countryCode = null,
        string? currencyCode = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            query.Add($"country={Uri.EscapeDataString(countryCode.Trim().ToUpperInvariant())}");
        }

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            query.Add($"currency={Uri.EscapeDataString(currencyCode.Trim().ToUpperInvariant())}");
        }

        var endpoint = "/api/external/bank" + (query.Count == 0 ? "" : $"?{string.Join("&", query)}");

        return SendAsync<object, List<BlaaizBankData>>(
            HttpMethod.Get,
            endpoint,
            null,
            JsonSerializer.Serialize(new { countryCode, currencyCode }),
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizResolveBankAccountResponse>> ResolveBankAccountAsync(
        BlaaizResolveBankAccountRequest request,
        CancellationToken ct = default)
    {
        var auditBody = JsonSerializer.Serialize(new
        {
            account_number = MaskSensitive(request.AccountNumber),
            bank_id = request.BankId
        });

        return SendAsync<BlaaizResolveBankAccountRequest, BlaaizResolveBankAccountResponse>(
            HttpMethod.Post,
            "/api/external/bank/account-lookup",
            request,
            auditBody,
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizWebhookReplayResponse>> ReplayWebhookAsync(
        BlaaizWebhookReplayRequest request,
        CancellationToken ct = default)
    {
        return SendAsync<BlaaizWebhookReplayRequest, BlaaizWebhookReplayResponse>(
            HttpMethod.Post,
            "/api/external/webhook-replay",
            request,
            JsonSerializer.Serialize(request, SerializerOptions),
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizRefundEnvelope>> InitiateRefundAsync(
        BlaaizRefundRequest request,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default)
    {
        return SendAsync<BlaaizRefundRequest, BlaaizRefundEnvelope>(
            HttpMethod.Post,
            "/api/external/refund",
            request,
            JsonSerializer.Serialize(request, SerializerOptions),
            transferId,
            collectionId,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizRefundEnvelope>> GetRefundAsync(
        string providerRefundId,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerRefundId))
        {
            throw new InvalidOperationException("Provider refund ID is required.");
        }

        var endpoint = $"/api/external/refund/{Uri.EscapeDataString(providerRefundId.Trim())}";

        return SendAsync<object, BlaaizRefundEnvelope>(
            HttpMethod.Get,
            endpoint,
            null,
            JsonSerializer.Serialize(new { providerRefundId }),
            transferId,
            collectionId,
            null,
            null,
            ct);
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
            id_number = MaskSensitive(request.IdNumber ?? ""),
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

    public Task<BlaaizApiResult<BlaaizKycUploadUrlResponse>> RequestKycUploadUrlAsync(
        BlaaizKycUploadUrlRequest request,
        Guid customerProfileId,
        CancellationToken ct = default)
    {
        var auditBody = JsonSerializer.Serialize(new
        {
            customer_id = request.CustomerId,
            file_category = request.FileCategory,
            customerProfileId
        });

        return SendAsync<BlaaizKycUploadUrlRequest, BlaaizKycUploadUrlResponse>(
            HttpMethod.Post,
            "/api/external/file/get-presigned-url",
            request,
            auditBody,
            null,
            null,
            null,
            RedactUploadUrlResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizMessageResponse>> AttachCustomerFilesAsync(
        string providerCustomerId,
        BlaaizAttachCustomerFilesRequest request,
        Guid customerProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}/files";
        var auditBody = JsonSerializer.Serialize(new
        {
            customerProfileId,
            providerCustomerId,
            id_file = request.IdentityFileId,
            id_file_back = request.IdentityBackFileId,
            proof_of_address_file = request.ProofOfAddressFileId,
            liveness_check_file = request.LivenessCheckFileId
        });

        return SendAsync<BlaaizAttachCustomerFilesRequest, BlaaizMessageResponse>(
            HttpMethod.Post,
            endpoint,
            request,
            auditBody,
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizCustomerEnvelope>> CreateBusinessCustomerAsync(
        BlaaizCreateCustomerRequest request,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var auditBody = CreateBusinessCustomerAuditBody(request, businessProfileId);

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

    public Task<BlaaizApiResult<BlaaizCustomerEnvelope>> UpdateBusinessCustomerAsync(
        string providerCustomerId,
        BlaaizCreateCustomerRequest request,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}";
        var auditBody = CreateBusinessCustomerAuditBody(request, businessProfileId);

        return SendAsync<BlaaizCreateCustomerRequest, BlaaizCustomerEnvelope>(
            HttpMethod.Put,
            endpoint,
            request,
            auditBody,
            null,
            null,
            null,
            RedactCustomerResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizCustomerEnvelope>> GetBusinessCustomerAsync(
        string providerCustomerId,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}";

        return SendAsync<object, BlaaizCustomerEnvelope>(
            HttpMethod.Get,
            endpoint,
            null,
            JsonSerializer.Serialize(new { businessProfileId, providerCustomerId }),
            null,
            null,
            null,
            RedactCustomerResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizUploadUrlEnvelope>> RequestBusinessOwnerUploadUrlAsync(
        string providerCustomerId,
        string providerOwnerId,
        BlaaizOwnerUploadUrlRequest request,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}/owner/{Uri.EscapeDataString(providerOwnerId)}/file/presigned-url";

        return SendAsync<BlaaizOwnerUploadUrlRequest, BlaaizUploadUrlEnvelope>(
            HttpMethod.Post,
            endpoint,
            request,
            JsonSerializer.Serialize(new
            {
                businessProfileId,
                providerCustomerId,
                providerOwnerId,
                file_category = request.FileCategory
            }),
            null,
            null,
            null,
            RedactUploadUrlResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizBusinessOwnerEnvelope>> AttachBusinessOwnerFilesAsync(
        string providerCustomerId,
        string providerOwnerId,
        BlaaizOwnerFilesRequest request,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}/owner/{Uri.EscapeDataString(providerOwnerId)}/files";

        return SendAsync<BlaaizOwnerFilesRequest, BlaaizBusinessOwnerEnvelope>(
            HttpMethod.Post,
            endpoint,
            request,
            JsonSerializer.Serialize(new
            {
                businessProfileId,
                providerCustomerId,
                providerOwnerId,
                id_document_front = request.IdDocumentFront,
                id_document_back = request.IdDocumentBack
            }),
            null,
            null,
            null,
            RedactCustomerResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizUploadUrlEnvelope>> RequestBusinessDocumentUploadUrlAsync(
        string providerCustomerId,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}/document/presigned-url";

        return SendAsync<object, BlaaizUploadUrlEnvelope>(
            HttpMethod.Post,
            endpoint,
            null,
            JsonSerializer.Serialize(new { businessProfileId, providerCustomerId }),
            null,
            null,
            null,
            RedactUploadUrlResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizBusinessDocumentEnvelope>> RegisterBusinessDocumentAsync(
        string providerCustomerId,
        BlaaizBusinessDocumentRequest request,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}/document";

        return SendAsync<BlaaizBusinessDocumentRequest, BlaaizBusinessDocumentEnvelope>(
            HttpMethod.Post,
            endpoint,
            request,
            JsonSerializer.Serialize(new
            {
                businessProfileId,
                providerCustomerId,
                type = request.Type,
                name = request.Name,
                file_id = request.FileId,
                description = request.Description
            }),
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizCustomerEnvelope>> SubmitBusinessCustomerAsync(
        string providerCustomerId,
        Guid businessProfileId,
        CancellationToken ct = default)
    {
        var endpoint = $"/api/external/customer/{Uri.EscapeDataString(providerCustomerId)}/submit";

        return SendAsync<object, BlaaizCustomerEnvelope>(
            HttpMethod.Post,
            endpoint,
            null,
            JsonSerializer.Serialize(new { businessProfileId, providerCustomerId }),
            null,
            null,
            null,
            RedactCustomerResponse,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizVirtualBankAccountEnvelope>> CreateVirtualBankAccountAsync(
        BlaaizVirtualBankAccountRequest request,
        Guid? businessCustomerId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WalletId))
            throw new InvalidOperationException("Blaaiz wallet ID is required.");

        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new InvalidOperationException("Blaaiz customer ID is required.");

        return SendAsync<BlaaizVirtualBankAccountRequest, BlaaizVirtualBankAccountEnvelope>(
            HttpMethod.Post,
            "/api/external/virtual-bank-account",
            request,
            JsonSerializer.Serialize(new
            {
                wallet_id = request.WalletId,
                customer_id = request.CustomerId,
                businessCustomerId
            }),
            null,
            null,
            null,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizVirtualBankAccountEnvelope>> GetVirtualBankAccountsAsync(
        string walletId,
        string customerId,
        Guid? businessCustomerId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(walletId))
            throw new InvalidOperationException("Blaaiz wallet ID is required.");

        if (string.IsNullOrWhiteSpace(customerId))
            throw new InvalidOperationException("Blaaiz customer ID is required.");

        var endpoint =
            "/api/external/virtual-bank-account" +
            $"?wallet_id={Uri.EscapeDataString(walletId.Trim())}" +
            $"&customer_id={Uri.EscapeDataString(customerId.Trim())}";

        return SendAsync<object, BlaaizVirtualBankAccountEnvelope>(
            HttpMethod.Get,
            endpoint,
            null,
            JsonSerializer.Serialize(new { walletId, customerId, businessCustomerId }),
            null,
            null,
            null,
            null,
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

    public Task<BlaaizApiResult<BlaaizPayoutResponse>> InitiatePayoutAsync(
        BlaaizPayoutRequest request,
        Guid? transferId,
        Guid payoutId,
        CancellationToken ct = default)
    {
        var auditBody = JsonSerializer.Serialize(new
        {
            wallet_id = request.WalletId,
            customer_id = request.CustomerId,
            method = request.Method,
            from_currency_id = request.FromCurrencyId,
            to_currency_id = request.ToCurrencyId,
            from_amount = request.FromAmount,
            to_amount = request.ToAmount,
            type = request.RecipientType,
            phone_number = request.PhoneNumber,
            email = request.Email,
            interac_first_name = request.InteracFirstName,
            interac_last_name = request.InteracLastName,
            bank_id = request.BankId,
            bank_name = request.BankName,
            account_name = request.AccountName,
            account_number = MaskSensitive(request.AccountNumber ?? ""),
            routing_number = MaskSensitive(request.RoutingNumber ?? ""),
            swift_code = MaskSensitive(request.SwiftCode ?? ""),
            sort_code = MaskSensitive(request.SortCode ?? ""),
            iban = MaskSensitive(request.Iban ?? ""),
            country = request.Country,
            wallet_address = MaskSensitive(request.WalletAddress ?? ""),
            wallet_token = request.WalletToken,
            wallet_network = request.WalletNetwork,
            note = request.Note
        }, SerializerOptions);

        return SendAsync<BlaaizPayoutRequest, BlaaizPayoutResponse>(
            HttpMethod.Post,
            "/api/external/payout",
            request,
            auditBody,
            transferId,
            null,
            payoutId,
            null,
            ct);
    }

    public Task<BlaaizApiResult<BlaaizTransactionEnvelope>> GetTransactionAsync(
        string providerTransactionIdOrReference,
        Guid? transferId = null,
        Guid? collectionId = null,
        Guid? payoutId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerTransactionIdOrReference))
        {
            throw new InvalidOperationException("Provider transaction ID or reference is required.");
        }

        var endpoint = $"/api/external/transaction/{Uri.EscapeDataString(providerTransactionIdOrReference.Trim())}";

        return SendAsync<object, BlaaizTransactionEnvelope>(
            HttpMethod.Get,
            endpoint,
            null,
            JsonSerializer.Serialize(new
            {
                providerTransactionIdOrReference,
                transferId,
                collectionId,
                payoutId
            }),
            transferId,
            collectionId,
            payoutId,
            null,
            ct);
    }

    private static string CreateBusinessCustomerAuditBody(
        BlaaizCreateCustomerRequest request,
        Guid businessProfileId)
    {
        return JsonSerializer.Serialize(new
        {
            businessProfileId,
            request.Type,
            request.BusinessName,
            request.TradingName,
            request.BusinessType,
            request.RegistrationNumber,
            request.IncorporationCountry,
            request.IncorporationDate,
            request.IndustryType,
            request.BusinessDescription,
            request.Website,
            request.SourceOfFunds,
            request.EstimatedAnnualRevenue,
            request.ExpectedMonthlyPayments,
            request.AccountPurpose,
            request.KybScope,
            request.Email,
            request.Country,
            request.Phone,
            tin = MaskSensitive(request.Tin ?? ""),
            request.Street,
            request.City,
            request.State,
            request.ZipCode,
            request.OperatingCountry,
            request.OperatingStreet,
            request.OperatingCity,
            request.OperatingState,
            request.OperatingZipCode,
            owners = request.Owners?.Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName,
                x.Email,
                x.DateOfBirth,
                x.Nationality,
                x.Country,
                x.Title,
                x.OwnershipPercentage,
                x.HasControl,
                x.IsSigner,
                x.IsBeneficialOwner,
                x.IdDocumentType,
                id_document_number = MaskSensitive(x.IdDocumentNumber),
                x.IdDocumentCountry,
                x.IdExpiryDate,
                x.IsPep
            })
        }, SerializerOptions);
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
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? additionalHeaders = null)
    {
        var token = await _tokenService.GetAccessTokenAsync(ct);

        var requestLogId = await _auditService.StartAsync(
            ProviderCode.Blaaiz,
            endpoint,
            method.Method,
            JsonSerializer.Serialize(new
            {
                authorization = "Bearer ***REDACTED***",
                contentType = requestBody is null ? null : "application/json",
                idempotencyKey = additionalHeaders?.ContainsKey("Idempotency-Key") == true
                    ? "***PRESENT***"
                    : null
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

            if (additionalHeaders is not null)
            {
                foreach (var header in additionalHeaders)
                {
                    if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Authorization header overrides are not allowed.");

                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

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
                    requestLogId,
                    validationErrors: BlaaizValidationErrors.Read(endpoint, (int)response.StatusCode, rawResponse));
            }

            TResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<TResponse>(rawResponse, SerializerOptions);
            }
            catch (JsonException ex)
            {
                var message = $"Blaaiz returned an incompatible JSON response at {ex.Path ?? "$"}.";

                await _auditService.FailAsync(
                    requestLogId,
                    (int)response.StatusCode,
                    auditResponse,
                    $"{message} {ex.Message}",
                    stopwatch.ElapsedMilliseconds,
                    ct);

                throw new ProviderIntegrationException(
                    message,
                    (int)response.StatusCode,
                    auditResponse,
                    requestLogId,
                    ex);
            }
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

    private static string RedactCustomerResponse(string rawResponse) =>
        RedactJsonProperties(
            rawResponse,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "id_number",
                "id_document_number",
                "tin",
                "tax_identification_number"
            },
            "***REDACTED***");

    private static string RedactUploadUrlResponse(string rawResponse) =>
        RedactJsonProperties(
            rawResponse,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "url" },
            "***PRESIGNED_URL_REDACTED***");

    private static string RedactJsonProperties(
        string rawJson,
        IReadOnlySet<string> propertyNames,
        string replacement)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is null) return rawJson;

            RedactNode(node, propertyNames, replacement);
            return node.ToJsonString(SerializerOptions);
        }
        catch (JsonException)
        {
            return rawJson;
        }
    }

    private static void RedactNode(
        JsonNode node,
        IReadOnlySet<string> propertyNames,
        string replacement)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToList())
            {
                if (propertyNames.Contains(key))
                {
                    obj[key] = replacement;
                    continue;
                }

                if (obj[key] is JsonNode child)
                {
                    RedactNode(child, propertyNames, replacement);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    RedactNode(child, propertyNames, replacement);
                }
            }
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
