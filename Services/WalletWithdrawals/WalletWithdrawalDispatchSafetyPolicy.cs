using KorridorX.Configuration;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Providers.Remittance;

namespace KorridorX.Services.WalletWithdrawals;

// Production can execute supported fiat withdrawal rails against the Blaaiz
// production endpoint. Development must use DEV; Staging may use either verified
// environment. Provider, account, compliance and ledger guards remain mandatory.
public static class WalletWithdrawalDispatchSafetyPolicy
{
    // Sandbox acceptance cannot be accidentally broadened into bulk payouts.
    // These are wallet-currency units, not converted values.
    public const decimal AbsoluteMaxNgnDebit = 50000m;
    public const decimal AbsoluteMaxCadDebit = 100m;

    public static bool CanInitiate(string environment, WalletWithdrawalProcessingOptions settings) =>
        settings.Enabled && (environment == Environments.Development ||
                             environment == Environments.Staging ||
                             environment == Environments.Production);

    // The prior W4 sandbox one-ID allowlist is no longer an execution gate.
    // It remains as a backward-compatible diagnostic-only helper.
    public static bool IsWithinConfiguredScope(Guid withdrawalId, string environment,
        WalletWithdrawalProcessingOptions settings) =>
        withdrawalId != Guid.Empty && CanInitiate(environment, settings);

    public static bool IsWithinExecutionDebitLimit(FiatWalletWithdrawal row, string environment,
        WalletWithdrawalProcessingOptions settings) =>
        row is not null && IsWithinConfiguredScope(row.Id, environment, settings) &&
        row.TotalWalletDebit > 0m && (row.CurrencyCode.ToUpperInvariant() is
            "NGN" or "CAD" or "USD" or "EUR" or "GBP");

    public static bool CanDispatch(string environment, WalletWithdrawalProcessingOptions settings,
        BlaaizOptions blaaiz) =>
        BaseAllowed(environment, settings, blaaiz) && settings.DispatchEnabled;

    public static bool CanRecover(string environment, WalletWithdrawalProcessingOptions settings,
        BlaaizOptions blaaiz) =>
        BaseAllowed(environment, settings, blaaiz) && settings.RecoveryEnabled;

    private static bool BaseAllowed(string environment, WalletWithdrawalProcessingOptions settings,
        BlaaizOptions blaaiz)
    {
        if (!CanInitiate(environment, settings) || !blaaiz.IsEnabled ||
            !Uri.TryCreate(blaaiz.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
            uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
            return false;
        var host = uri.Host.ToLowerInvariant();
        if (string.Equals(environment, Environments.Development, StringComparison.Ordinal))
            return host == "api-dev.blaaiz.com";
        if (string.Equals(environment, Environments.Staging, StringComparison.Ordinal))
            return host is "api-dev.blaaiz.com" or "api-prod.blaaiz.com";
        if (string.Equals(environment, Environments.Production, StringComparison.Ordinal))
            return host == "api-prod.blaaiz.com";
        return false;
    }

    private static bool HasExactSandboxAllowlist(WalletWithdrawalProcessingOptions settings)
    {
        var ids = settings.SandboxAllowedWithdrawalIds;
        return ids is { Count: 1 } && ids.All(x => x != Guid.Empty) &&
            ids.Distinct().Count() == ids.Count;
    }

    public static bool IsSandboxAllowlisted(Guid withdrawalId, WalletWithdrawalProcessingOptions settings) =>
        withdrawalId != Guid.Empty && HasExactSandboxAllowlist(settings) &&
        settings.SandboxAllowedWithdrawalIds.Contains(withdrawalId);

    public static bool IsWithinSandboxDebitCeiling(FiatWalletWithdrawal row,
        WalletWithdrawalProcessingOptions settings)
    {
        if (row is null || !IsSandboxAllowlisted(row.Id, settings) || row.TotalWalletDebit <= 0m)
            return false;

        var (configured, hard) = row.CurrencyCode.ToUpperInvariant() switch
        {
            "NGN" => (settings.SandboxMaxNgnDebit, AbsoluteMaxNgnDebit),
            "CAD" => (settings.SandboxMaxCadDebit, AbsoluteMaxCadDebit),
            _ => (0m, 0m)
        };
        return configured > 0m && configured <= hard && row.TotalWalletDebit <= configured;
    }

    public static bool CanClaim(FiatWalletWithdrawal row) =>
        !row.IsDeleted && row.Status == FiatWalletWithdrawalStatus.AwaitingExecution &&
        !row.PayoutId.HasValue && string.IsNullOrWhiteSpace(row.OperationalHoldReason);

    public static bool CanCancelUnsubmitted(FiatWalletWithdrawal row) =>
        !row.IsDeleted && row.Status == FiatWalletWithdrawalStatus.AwaitingExecution &&
        !row.PayoutId.HasValue && !row.SubmittedToProviderAtUtc.HasValue;

    public static void ValidateSnapshot(FiatWalletWithdrawal row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var permitted = row.CurrencyCode.ToUpperInvariant() switch
        {
            "NGN" => row.PaymentMethod == PaymentMethod.BankTransfer &&
                !string.IsNullOrWhiteSpace(row.ProviderBankId) &&
                !string.IsNullOrWhiteSpace(row.ProtectedAccountNumber) &&
                !string.IsNullOrWhiteSpace(row.VerifiedAccountName),
            "CAD" => row.PaymentMethod == PaymentMethod.Interac &&
                !string.IsNullOrWhiteSpace(row.RecipientEmail) &&
                !string.IsNullOrWhiteSpace(row.VerifiedAccountName),
            "USD" => (row.PaymentMethod is PaymentMethod.Ach or PaymentMethod.Wire) &&
                !string.IsNullOrWhiteSpace(row.ProtectedAccountNumber) &&
                !string.IsNullOrWhiteSpace(row.VerifiedAccountName),
            "EUR" or "GBP" => row.PaymentMethod == PaymentMethod.BankTransfer &&
                !string.IsNullOrWhiteSpace(row.ProtectedAccountNumber) &&
                !string.IsNullOrWhiteSpace(row.VerifiedAccountName),
            _ => false
        };
        if (!permitted || row.FinancialReservationId is null ||
            row.TotalWalletDebit <= 0m || row.WithdrawalAmount != row.TotalWalletDebit ||
            row.ExpectedRecipientAmount <= 0m || row.ProviderFee < 0m || row.KorridorXFee != 0m ||
            row.ExpectedRecipientAmount + row.ProviderFee != row.TotalWalletDebit ||
            row.KorridorXFeePolicy != "NOT_CONFIGURED" ||
            string.IsNullOrWhiteSpace(row.Reference))
            throw new InvalidOperationException("Withdrawal financial or destination snapshot is not safe to dispatch.");
    }

    public static RemittancePayoutRequest BuildRequest(FiatWalletWithdrawal row, Guid payoutId,
        string providerCustomerId, string providerWalletId, string? unprotectedAccountNumber)
    {
        ValidateSnapshot(row);
        if (payoutId == Guid.Empty || string.IsNullOrWhiteSpace(providerCustomerId) ||
            string.IsNullOrWhiteSpace(providerWalletId))
            throw new InvalidOperationException("Verified payout identity and wallet are required.");
        if (row.CurrencyCode == "NGN" &&
            (string.IsNullOrWhiteSpace(unprotectedAccountNumber) ||
             unprotectedAccountNumber.Length != 10 || !unprotectedAccountNumber.All(char.IsAsciiDigit)))
            throw new InvalidOperationException("A verified NGN bank-account number is required.");
        var foreign = WalletWithdrawalForeignAccount.Supports(row.CurrencyCode, row.PaymentMethod)
            ? WalletWithdrawalForeignAccount.Parse(unprotectedAccountNumber, row.CurrencyCode, row.PaymentMethod)
            : null;
        if (foreign is not null && !string.Equals(foreign.AccountName, row.VerifiedAccountName, StringComparison.Ordinal))
            throw new InvalidOperationException("The saved bank account name differs from its immutable withdrawal snapshot.");

        var words = row.VerifiedAccountName!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
            throw new InvalidOperationException("A verified first and last recipient name are required.");
        var first = words[0];
        var last = string.Join(" ", words.Skip(1));
        return new RemittancePayoutRequest(
            TransferId: null, PayoutId: payoutId, PaymentMethod: row.PaymentMethod,
            DestinationAmount: row.ExpectedRecipientAmount,
            SourceCurrencyCode: row.CurrencyCode, DestinationCurrencyCode: row.CurrencyCode,
            ProviderCustomerId: providerCustomerId, WalletId: providerWalletId,
            RecipientFirstName: first, RecipientLastName: last,
            RecipientEmail: row.PaymentMethod == PaymentMethod.Interac ? row.RecipientEmail : null,
            RecipientPhoneNumber: null, BankId: row.ProviderBankId, BankName: row.BankName,
            AccountName: row.VerifiedAccountName, AccountNumber: foreign?.AccountNumber ?? unprotectedAccountNumber,
            RoutingNumber: foreign?.RoutingNumber, SortCode: foreign?.SortCode,
            Iban: foreign?.Iban, SwiftBic: foreign?.SwiftCode,
            Note: "KorridorX wallet withdrawal " + row.Reference,
            FromAmount: row.TotalWalletDebit, MerchantReference: row.Reference,
            RecipientType: foreign?.RecipientType, AccountType: foreign?.AccountType,
            RecipientCountry: foreign?.Country, RecipientState: foreign?.State,
            RecipientStreet: foreign?.Street, RecipientCity: foreign?.City,
            RecipientPostalCode: foreign?.ZipCode, BicCode: foreign?.BicCode);
    }
}
