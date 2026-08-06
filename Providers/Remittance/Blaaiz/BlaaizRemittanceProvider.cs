using KorridorX.Configuration;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Remittance.Blaaiz;

public class BlaaizRemittanceProvider : IRemittanceProvider
{
    private static readonly HashSet<string> AllowedIndividualIdTypes =
    [
        "drivers_license",
        "passport",
        "resident_permit"
    ];

    private readonly IBlaaizApiClient _apiClient;
    private readonly IBlaaizTokenService _tokenService;
    private readonly BlaaizOptions _options;

    public BlaaizRemittanceProvider(
        IBlaaizApiClient apiClient,
        IBlaaizTokenService tokenService,
        IOptions<BlaaizOptions> options)
    {
        _apiClient = apiClient;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public string ProviderName => "Blaaiz";
    public ProviderCode ProviderCode => ProviderCode.Blaaiz;

    public async Task<IReadOnlyList<RemittanceBank>> GetBanksAsync(
        string? countryCode = null,
        string? currencyCode = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.ListBanksAsync(countryCode, currencyCode, ct);

        return response.Data
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new RemittanceBank(
                x.Id,
                x.Name,
                x.Code,
                x.NationalBankCode,
                x.CountryId,
                x.Country?.Code?.ToUpperInvariant() ?? countryCode?.Trim().ToUpperInvariant() ?? "",
                x.Country?.Name,
                System.Text.Json.JsonSerializer.Serialize(x)))
            .ToList();
    }

    public async Task<RemittanceBankAccountResolutionResult> ResolveBankAccountAsync(
        string providerBankId,
        string accountNumber,
        CancellationToken ct = default)
    {
        var bankId = NormalizeRequired(providerBankId, "Provider bank ID");
        var number = NormalizeRequired(accountNumber, "Account number");

        var response = await _apiClient.ResolveBankAccountAsync(
            new BlaaizResolveBankAccountRequest
            {
                BankId = bankId,
                AccountNumber = number
            },
            ct);

        return new RemittanceBankAccountResolutionResult(
            bankId,
            number,
            NormalizeRequired(response.Data.AccountName, "Resolved account name"),
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceWebhookReplayResult> ReplayWebhookAsync(
        string providerTransactionId,
        CancellationToken ct = default)
    {
        var transactionId = NormalizeRequired(providerTransactionId, "Provider transaction ID");
        var response = await _apiClient.ReplayWebhookAsync(
            new BlaaizWebhookReplayRequest { TransactionId = transactionId },
            ct);

        return new RemittanceWebhookReplayResult(
            transactionId,
            response.Data.Message,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceRefundResult> InitiateRefundAsync(
        string providerCollectionTransactionId,
        string reference,
        string? reason = null,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.InitiateRefundAsync(
            new BlaaizRefundRequest
            {
                TransactionId = NormalizeRequired(providerCollectionTransactionId, "Provider collection transaction ID"),
                Reference = NormalizeRequired(reference, "Refund reference"),
                Reason = NormalizeOptional(reason)
            },
            transferId,
            collectionId,
            ct);

        return MapRefund(response);
    }

    public async Task<RemittanceRefundResult> GetRefundAsync(
        string providerRefundId,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.GetRefundAsync(
            NormalizeRequired(providerRefundId, "Provider refund ID"),
            transferId,
            collectionId,
            ct);

        return MapRefund(response);
    }

    private static RemittanceRefundResult MapRefund(
        BlaaizApiResult<BlaaizRefundEnvelope> response)
    {
        var refund = response.Data.Data;
        return new RemittanceRefundResult(
            refund.Id,
            refund.Status,
            refund.Amount,
            refund.Currency,
            refund.TransactionId,
            refund.Reference,
            refund.RefundReference,
            refund.FailureReason,
            refund.CreatedAt,
            refund.UpdatedAt,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!_options.IsEnabled)
        {
            return false;
        }

        try
        {
            _ = await _tokenService.GetAccessTokenAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<RemittanceProviderCustomerResult> SyncIndividualCustomerAsync(
        RemittanceProviderCustomerRequest request,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.ExistingProviderCustomerId))
        {
            var existing = await _apiClient.GetCustomerAsync(
                request.ExistingProviderCustomerId,
                request.CustomerProfileId,
                ct);

            return new RemittanceProviderCustomerResult(
                existing.Data.Data.Id,
                existing.Data.Data.VerificationStatus,
                existing.RawResponseJson,
                existing.RequestLogId);
        }

        var idType = NormalizeRequired(request.IdType, "Identification type").ToLowerInvariant();
        if (!AllowedIndividualIdTypes.Contains(idType))
        {
            throw new InvalidOperationException(
                "Identification type must be drivers_license, passport, or resident_permit.");
        }

        var idNumber = NormalizeRequired(request.IdNumber, "Identification number");

        var providerRequest = new BlaaizCreateCustomerRequest
        {
            Type = "individual",
            FirstName = NormalizeRequired(request.FirstName, "First name"),
            LastName = NormalizeRequired(request.LastName, "Last name"),
            Email = NormalizeRequired(request.Email, "Email").ToLowerInvariant(),
            Country = NormalizeRequired(request.CountryCode, "Country code").ToUpperInvariant(),
            IdType = idType,
            IdNumber = idNumber,
            Phone = NormalizeOptional(request.PhoneNumber),
            DateOfBirth = FormatDate(request.DateOfBirth),
            Street = NormalizeOptional(request.Street),
            City = NormalizeOptional(request.City),
            State = NormalizeOptional(request.State),
            ZipCode = NormalizeOptional(request.PostalCode),
            IdIssueDate = FormatDate(request.IdIssueDate),
            IdExpiryDate = FormatDate(request.IdExpiryDate)
        };

        var created = await _apiClient.CreateCustomerAsync(
            providerRequest,
            request.CustomerProfileId,
            ct);

        return new RemittanceProviderCustomerResult(
            created.Data.Data.Id,
            created.Data.Data.VerificationStatus,
            created.RawResponseJson,
            created.RequestLogId);
    }

    public async Task<RemittanceKycUploadUrlResult> RequestIndividualKycUploadUrlAsync(
        RemittanceKycUploadUrlRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderCustomerId))
        {
            throw new InvalidOperationException("Provider customer ID is required before requesting a KYC upload URL.");
        }

        var response = await _apiClient.RequestKycUploadUrlAsync(
            new BlaaizKycUploadUrlRequest
            {
                CustomerId = request.ProviderCustomerId,
                FileCategory = MapFileCategory(request.DocumentType)
            },
            request.CustomerProfileId,
            ct);

        return new RemittanceKycUploadUrlResult(
            response.Data.FileId,
            response.Data.Url,
            response.Data.Headers,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceKycDocumentSubmissionResult> SubmitIndividualKycDocumentsAsync(
        RemittanceKycDocumentSubmissionRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityFileId))
        {
            throw new InvalidOperationException("An uploaded identity-front file is required before KYC submission.");
        }

        var response = await _apiClient.AttachCustomerFilesAsync(
            request.ProviderCustomerId,
            new BlaaizAttachCustomerFilesRequest
            {
                IdentityFileId = request.IdentityFileId,
                IdentityBackFileId = request.IdentityBackFileId,
                ProofOfAddressFileId = request.ProofOfAddressFileId,
                LivenessCheckFileId = request.LivenessCheckFileId
            },
            request.CustomerProfileId,
            ct);

        return new RemittanceKycDocumentSubmissionResult(
            "PENDING",
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceCollectionResult> InitiateCollectionAsync(
        RemittanceCollectionRequest request,
        CancellationToken ct = default)
    {
        return request.PaymentMethod switch
        {
            PaymentMethod.Card => await InitiateCardCollectionAsync(request, ct),
            PaymentMethod.Interac => await InitiateInteracCollectionAsync(request, ct),
            _ => throw new InvalidOperationException(
                $"Live Blaaiz initiation is not yet implemented for '{request.PaymentMethod}'.")
        };
    }

    private async Task<RemittanceCollectionResult> InitiateCardCollectionAsync(
        RemittanceCollectionRequest request,
        CancellationToken ct)
    {
        if (request.CardDetails is null)
        {
            throw new InvalidOperationException("Card details are required for a card collection.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCustomerId))
        {
            throw new InvalidOperationException(
                "A synchronized and verified Blaaiz customer is required for card collections.");
        }

        var walletId = request.WalletId;
        if (string.IsNullOrWhiteSpace(walletId))
        {
            throw new InvalidOperationException(
                $"No Blaaiz collection wallet is configured for {request.CurrencyCode}.");
        }

        var cardNumber = new string(request.CardDetails.CardNumber.Where(char.IsDigit).ToArray());
        if (cardNumber.Length != 16)
        {
            throw new InvalidOperationException("Card number must contain exactly 16 digits.");
        }

        var cvc = new string(request.CardDetails.Cvc.Where(char.IsDigit).ToArray());
        if (cvc.Length != 3)
        {
            throw new InvalidOperationException("Card CVC must contain exactly 3 digits.");
        }

        var expiry = NormalizeRequired(request.CardDetails.Expiry, "Card expiry");
        if (!System.Text.RegularExpressions.Regex.IsMatch(expiry, "^(0[1-9]|1[0-2])/\\d{2}$"))
        {
            throw new InvalidOperationException("Card expiry must use MM/YY format.");
        }

        if (!string.IsNullOrWhiteSpace(request.RedirectUrl) &&
            (!Uri.TryCreate(request.RedirectUrl, UriKind.Absolute, out var redirectUri) ||
             redirectUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Redirect URL must be a valid HTTPS URL.");
        }

        var providerRequest = new BlaaizCardCollectionRequest
        {
            Amount = request.Amount,
            WalletId = walletId,
            CustomerId = request.ProviderCustomerId,
            CardHolderName = NormalizeRequired(request.CardDetails.CardHolderName, "Card holder name"),
            CardNumber = cardNumber,
            Expiry = expiry,
            Cvc = cvc,
            Phone = NormalizeOptional(request.PhoneNumber),
            RedirectUrl = NormalizeOptional(request.RedirectUrl)
        };

        var response = await _apiClient.InitiateCardCollectionAsync(
            providerRequest,
            request.TransferId,
            request.CollectionId,
            ct);

        return new RemittanceCollectionResult(
            response.Data.TransactionId,
            null,
            "PENDING",
            response.Data.Url,
            null,
            response.RawResponseJson,
            response.RequestLogId);
    }

    private async Task<RemittanceCollectionResult> InitiateInteracCollectionAsync(
        RemittanceCollectionRequest request,
        CancellationToken ct)
    {
        if (!string.Equals(request.CurrencyCode, "CAD", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Interac money requests are available only for CAD collections.");
        }

        if (request.InteracExpiryHours is < 1 or > 120)
        {
            throw new InvalidOperationException("Interac expiry hours must be between 1 and 120.");
        }

        var providerRequest = new BlaaizInteracMoneyRequest
        {
            Amount = request.Amount,
            Email = NormalizeRequired(request.CustomerEmail, "Payer email").ToLowerInvariant(),
            CustomerName = NormalizeOptional(request.CustomerName),
            CustomerId = NormalizeOptional(request.ProviderCustomerId),
            ExpiryHours = request.InteracExpiryHours
        };

        var response = await _apiClient.InitiateInteracMoneyRequestAsync(
            providerRequest,
            request.TransferId,
            request.CollectionId,
            ct);

        return new RemittanceCollectionResult(
            response.Data.TransactionId,
            response.Data.Reference,
            "PENDING",
            null,
            response.Data.ExpiresAt,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittancePayoutResult> InitiatePayoutAsync(
        RemittancePayoutRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WalletId))
        {
            throw new InvalidOperationException(
                $"No Blaaiz payout wallet is configured for {request.SourceCurrencyCode}.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCustomerId))
        {
            throw new InvalidOperationException("A verified Blaaiz customer is required before payout initiation.");
        }

        var providerRequest = new BlaaizPayoutRequest
        {
            WalletId = request.WalletId,
            CustomerId = request.ProviderCustomerId,
            Method = MapPayoutMethod(request.PaymentMethod),
            FromCurrencyId = request.SourceCurrencyCode.ToUpperInvariant(),
            ToCurrencyId = request.DestinationCurrencyCode.ToUpperInvariant(),
            ToAmount = request.DestinationAmount,
            Note = NormalizeOptional(request.Note)
        };

        switch (request.PaymentMethod)
        {
            case PaymentMethod.BankTransfer:
                providerRequest.BankId = NormalizeRequired(request.BankId, "Bank ID");
                providerRequest.AccountNumber = NormalizeRequired(request.AccountNumber, "Account number");
                providerRequest.AccountName = NormalizeOptional(request.AccountName);
                providerRequest.BankName = NormalizeOptional(request.BankName);
                providerRequest.SortCode = NormalizeOptional(request.SortCode);
                providerRequest.Iban = NormalizeOptional(request.Iban);
                providerRequest.BicCode = NormalizeOptional(request.SwiftBic);
                // Country is not required for the currently enabled NGN bank-transfer corridor.
                // Additional country-specific payout fields will be added when more corridors are enabled.
                providerRequest.Country = null;
                break;

            case PaymentMethod.Interac:
                providerRequest.Email = NormalizeRequired(request.RecipientEmail, "Recipient email").ToLowerInvariant();
                providerRequest.InteracFirstName = NormalizeRequired(request.RecipientFirstName, "Recipient first name");
                providerRequest.InteracLastName = NormalizeRequired(request.RecipientLastName, "Recipient last name");
                break;

            default:
                throw new InvalidOperationException(
                    $"Live Blaaiz payout initiation is not yet implemented for '{request.PaymentMethod}'.");
        }

        var response = await _apiClient.InitiatePayoutAsync(
            providerRequest,
            request.TransferId,
            request.PayoutId,
            ct);

        var transaction = response.Data.Transaction;
        if (string.IsNullOrWhiteSpace(transaction.Id))
        {
            throw new InvalidOperationException("Blaaiz did not return a payout transaction ID.");
        }

        return new RemittancePayoutResult(
            transaction.Id,
            transaction.Reference,
            transaction.Status,
            transaction.Recipient?.Currency ?? transaction.Currency,
            transaction.Recipient?.Amount ?? transaction.Amount,
            transaction.Question,
            transaction.Answer,
            transaction.Date,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceTransactionStatusResult> GetTransactionAsync(
        string providerTransactionIdOrReference,
        Guid? transferId = null,
        Guid? collectionId = null,
        Guid? payoutId = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.GetTransactionAsync(
            providerTransactionIdOrReference,
            transferId,
            collectionId,
            payoutId,
            ct);

        var transaction = response.Data.Data;

        var isPayout = string.Equals(transaction.Type, "payout", StringComparison.OrdinalIgnoreCase);

        return new RemittanceTransactionStatusResult(
            transaction.Id,
            transaction.Reference,
            transaction.Type,
            transaction.Status,
            isPayout ? transaction.Recipient?.Currency ?? transaction.Currency : transaction.Currency,
            isPayout ? transaction.Recipient?.Amount ?? transaction.Amount : transaction.Amount,
            transaction.FailureReason,
            transaction.Date,
            response.RawResponseJson,
            response.RequestLogId);
    }

    private static string MapPayoutMethod(PaymentMethod paymentMethod) => paymentMethod switch
    {
        PaymentMethod.BankTransfer => "bank_transfer",
        PaymentMethod.Interac => "interac",
        PaymentMethod.Ach => "ach",
        PaymentMethod.Wire => "wire",
        _ => throw new InvalidOperationException($"Unsupported Blaaiz payout method '{paymentMethod}'.")
    };

    private static string MapFileCategory(KycDocumentType documentType) => documentType switch
    {
        KycDocumentType.IdentityFront => "identity",
        KycDocumentType.IdentityBack => "identity_back",
        KycDocumentType.ProofOfAddress => "proof_of_address",
        KycDocumentType.LivenessCheck => "liveness_check",
        _ => throw new InvalidOperationException("Unsupported KYC document type.")
    };

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FormatDate(DateTime? value) =>
        value?.ToString("yyyy-MM-dd");
}
