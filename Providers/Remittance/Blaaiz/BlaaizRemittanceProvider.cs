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

    public async Task<RemittanceBusinessCustomerResult> SyncBusinessCustomerAsync(
        RemittanceBusinessCustomerRequest request,
        CancellationToken ct = default)
    {
        var providerRequest = new BlaaizCreateCustomerRequest
        {
            Type = "business",
            BusinessName = NormalizeRequired(request.BusinessName, "Business name"),
            TradingName = NormalizeOptional(request.TradingName),
            BusinessType = NormalizeOptional(request.BusinessType),
            RegistrationNumber = NormalizeRequired(request.RegistrationNumber, "Registration number"),
            IncorporationCountry = NormalizeRequired(request.IncorporationCountry, "Incorporation country").ToUpperInvariant(),
            IncorporationDate = FormatDate(request.IncorporationDate),
            IndustryType = NormalizeOptional(request.IndustryType),
            BusinessDescription = NormalizeOptional(request.BusinessDescription),
            Website = NormalizeOptional(request.Website),
            SourceOfFunds = NormalizeOptional(request.SourceOfFunds),
            EstimatedAnnualRevenue = NormalizeOptional(request.EstimatedAnnualRevenue),
            ExpectedMonthlyPayments = request.ExpectedMonthlyPayments,
            AccountPurpose = NormalizeOptional(request.AccountPurpose),
            KybScope = NormalizeRequired(request.KybScope, "KYB scope").ToUpperInvariant(),
            Email = NormalizeRequired(request.Email, "Business email").ToLowerInvariant(),
            Country = NormalizeRequired(request.CountryCode, "Registered country").ToUpperInvariant(),
            Phone = NormalizeOptional(request.Phone),
            Tin = NormalizeOptional(request.Tin),
            Street = NormalizeOptional(request.Street),
            City = NormalizeOptional(request.City),
            State = NormalizeOptional(request.State),
            ZipCode = NormalizeOptional(request.PostalCode),
            OperatingCountry = NormalizeOptional(request.OperatingCountry)?.ToUpperInvariant(),
            OperatingStreet = NormalizeOptional(request.OperatingStreet),
            OperatingCity = NormalizeOptional(request.OperatingCity),
            OperatingState = NormalizeOptional(request.OperatingState),
            OperatingZipCode = NormalizeOptional(request.OperatingPostalCode),
            Owners = request.Owners.Select(MapBusinessOwner).ToList()
        };

        if (!string.Equals(providerRequest.Country, providerRequest.IncorporationCountry, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Registered country must match incorporation country.");
        }

        BlaaizApiResult<BlaaizCustomerEnvelope> response;
        if (string.IsNullOrWhiteSpace(request.ExistingProviderCustomerId))
        {
            response = await _apiClient.CreateBusinessCustomerAsync(
                providerRequest,
                request.BusinessProfileId,
                ct);
        }
        else
        {
            response = await _apiClient.UpdateBusinessCustomerAsync(
                request.ExistingProviderCustomerId,
                providerRequest,
                request.BusinessProfileId,
                ct);
        }

        return MapBusinessCustomerResult(response, request.Owners);
    }

    public async Task<RemittanceBusinessUploadUrlResult> RequestBusinessOwnerUploadUrlAsync(
        RemittanceBusinessOwnerUploadUrlRequest request,
        CancellationToken ct = default)
    {
        var response = await _apiClient.RequestBusinessOwnerUploadUrlAsync(
            NormalizeRequired(request.ProviderCustomerId, "Provider customer ID"),
            NormalizeRequired(request.ProviderOwnerId, "Provider owner ID"),
            new BlaaizOwnerUploadUrlRequest
            {
                FileCategory = request.Side == BusinessOwnerDocumentSide.Front
                    ? "id_document_front"
                    : "id_document_back"
            },
            request.BusinessProfileId,
            ct);

        return new RemittanceBusinessUploadUrlResult(
            response.Data.Data.FileId,
            response.Data.Data.Url,
            response.Data.Data.Headers,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceBusinessOwnerFilesResult> SubmitBusinessOwnerFilesAsync(
        RemittanceBusinessOwnerFilesRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityFrontFileId) &&
            string.IsNullOrWhiteSpace(request.IdentityBackFileId))
        {
            throw new InvalidOperationException("At least one owner identity file is required.");
        }

        var response = await _apiClient.AttachBusinessOwnerFilesAsync(
            NormalizeRequired(request.ProviderCustomerId, "Provider customer ID"),
            NormalizeRequired(request.ProviderOwnerId, "Provider owner ID"),
            new BlaaizOwnerFilesRequest
            {
                IdDocumentFront = NormalizeOptional(request.IdentityFrontFileId),
                IdDocumentBack = NormalizeOptional(request.IdentityBackFileId)
            },
            request.BusinessProfileId,
            ct);

        return new RemittanceBusinessOwnerFilesResult(
            request.ProviderOwnerId,
            response.Data.Data.Status,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceBusinessUploadUrlResult> RequestBusinessDocumentUploadUrlAsync(
        RemittanceBusinessDocumentUploadUrlRequest request,
        CancellationToken ct = default)
    {
        var response = await _apiClient.RequestBusinessDocumentUploadUrlAsync(
            NormalizeRequired(request.ProviderCustomerId, "Provider customer ID"),
            request.BusinessProfileId,
            ct);

        return new RemittanceBusinessUploadUrlResult(
            response.Data.Data.FileId,
            response.Data.Data.Url,
            response.Data.Data.Headers,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceBusinessDocumentResult> RegisterBusinessDocumentAsync(
        RemittanceBusinessDocumentRegistrationRequest request,
        CancellationToken ct = default)
    {
        var response = await _apiClient.RegisterBusinessDocumentAsync(
            NormalizeRequired(request.ProviderCustomerId, "Provider customer ID"),
            new BlaaizBusinessDocumentRequest
            {
                Type = MapBusinessDocumentType(request.DocumentType),
                Name = NormalizeRequired(request.Name, "Document name"),
                FileId = NormalizeRequired(request.ProviderFileId, "Provider file ID"),
                Description = NormalizeOptional(request.Description)
            },
            request.BusinessProfileId,
            ct);

        return new RemittanceBusinessDocumentResult(
            response.Data.Data.Id,
            response.Data.Data.Type,
            response.Data.Data.Name,
            response.Data.Data.Status,
            SerializeOptional(response.Data.Data.AdminComments));
    }

    public async Task<RemittanceBusinessKybSubmissionResult> SubmitBusinessKybAsync(
        Guid businessProfileId,
        string providerCustomerId,
        CancellationToken ct = default)
    {
        var response = await _apiClient.SubmitBusinessCustomerAsync(
            NormalizeRequired(providerCustomerId, "Provider customer ID"),
            businessProfileId,
            ct);

        return new RemittanceBusinessKybSubmissionResult(
            response.Data.Data.VerificationStatus,
            response.RawResponseJson,
            response.RequestLogId);
    }

    public async Task<RemittanceBusinessCustomerResult> GetBusinessCustomerAsync(
        Guid businessProfileId,
        string providerCustomerId,
        CancellationToken ct = default)
    {
        var response = await _apiClient.GetBusinessCustomerAsync(
            NormalizeRequired(providerCustomerId, "Provider customer ID"),
            businessProfileId,
            ct);

        return MapBusinessCustomerResult(response, []);
    }

    private static BlaaizBusinessOwnerRequest MapBusinessOwner(RemittanceBusinessOwnerRequest owner)
    {
        var idType = NormalizeRequired(owner.IdDocumentType, "Owner identity document type").ToLowerInvariant();
        if (idType is not ("passport" or "drivers_license" or "resident_permit" or "id_card"))
        {
            throw new InvalidOperationException(
                "Owner identity document type must be passport, drivers_license, resident_permit, or id_card.");
        }

        return new BlaaizBusinessOwnerRequest
        {
            Id = NormalizeOptional(owner.ProviderOwnerId),
            FirstName = NormalizeRequired(owner.FirstName, "Owner first name"),
            LastName = NormalizeRequired(owner.LastName, "Owner last name"),
            Email = NormalizeRequired(owner.Email, "Owner email").ToLowerInvariant(),
            DateOfBirth = owner.DateOfBirth.ToString("yyyy-MM-dd"),
            Nationality = NormalizeRequired(owner.Nationality, "Owner nationality").ToUpperInvariant(),
            Country = NormalizeRequired(owner.CountryCode, "Owner country").ToUpperInvariant(),
            Title = NormalizeOptional(owner.Title),
            OwnershipPercentage = owner.OwnershipPercentage,
            HasControl = owner.HasControl,
            IsSigner = owner.IsSigner,
            IsBeneficialOwner = owner.IsBeneficialOwner,
            IdDocumentType = idType,
            IdDocumentNumber = NormalizeRequired(owner.IdDocumentNumber, "Owner identity document number"),
            IdDocumentCountry = NormalizeRequired(owner.IdDocumentCountry, "Owner identity document country").ToUpperInvariant(),
            IdExpiryDate = owner.IdExpiryDate.ToString("yyyy-MM-dd"),
            IsPep = owner.IsPep
        };
    }

    private static RemittanceBusinessCustomerResult MapBusinessCustomerResult(
        BlaaizApiResult<BlaaizCustomerEnvelope> response,
        IReadOnlyList<RemittanceBusinessOwnerRequest> requestedOwners)
    {
        var customer = response.Data.Data;
        var owners = customer.Owners.Select(providerOwner =>
        {
            var local = requestedOwners.FirstOrDefault(x =>
                (!string.IsNullOrWhiteSpace(providerOwner.Email) &&
                 string.Equals(x.Email, providerOwner.Email, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(x.FirstName, providerOwner.FirstName, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(x.LastName, providerOwner.LastName, StringComparison.OrdinalIgnoreCase) &&
                 x.OwnershipPercentage == providerOwner.OwnershipPercentage));

            return new RemittanceBusinessOwnerResult(
                local?.LocalOwnerId ?? Guid.Empty,
                providerOwner.Id,
                providerOwner.Status,
                SerializeOptional(providerOwner.AdminComments));
        }).ToList();

        var documents = customer.Documents.Select(x => new RemittanceBusinessDocumentResult(
            x.Id,
            x.Type,
            x.Name,
            x.Status,
            SerializeOptional(x.AdminComments))).ToList();

        return new RemittanceBusinessCustomerResult(
            customer.Id,
            customer.VerificationStatus,
            customer.KybScope,
            owners,
            documents,
            response.RawResponseJson,
            response.RequestLogId);
    }

    private static string MapBusinessDocumentType(BusinessKybDocumentType type) => type switch
    {
        BusinessKybDocumentType.CertificateOfIncorporation => "CERTIFICATE_OF_INCORPORATION",
        BusinessKybDocumentType.ArticlesOfIncorporation => "ARTICLES_OF_INCORPORATION",
        BusinessKybDocumentType.BeneficialOwnershipCertificate => "BENEFICIAL_OWNERSHIP_CERTIFICATE",
        BusinessKybDocumentType.IncorporationDocuments => "INCORPORATION_DOCUMENTS",
        BusinessKybDocumentType.CacStatusReport => "CAC_STATUS_REPORT",
        BusinessKybDocumentType.ShareRegister => "SHARE_REGISTER",
        BusinessKybDocumentType.BankStatement => "BANK_STATEMENT",
        BusinessKybDocumentType.ProofOfBusinessAddress => "PROOF_OF_ADDRESS",
        BusinessKybDocumentType.TaxDocument => "TAX_DOCUMENT",
        BusinessKybDocumentType.Other => "OTHER",
        _ => throw new InvalidOperationException("Unsupported business KYB document type.")
    };

    private static string? SerializeOptional(object? value) =>
        value is null ? null : System.Text.Json.JsonSerializer.Serialize(value);

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
