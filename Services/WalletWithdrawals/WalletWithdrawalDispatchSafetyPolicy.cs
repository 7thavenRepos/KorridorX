using KorridorX.Configuration;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Providers.Remittance;

namespace KorridorX.Services.WalletWithdrawals;

// All outbound provider calls stay Development-only and are individually
// disabled by default. In particular a local Development environment must
// never accidentally point a withdrawal worker at a live Blaaiz endpoint.
public static class WalletWithdrawalDispatchSafetyPolicy
{
    // Sandbox acceptance cannot be accidentally broadened into bulk payouts.
    // These are wallet-currency units, not converted values.
    public const decimal AbsoluteMaxNgnDebit = 50000m;
    public const decimal AbsoluteMaxCadDebit = 100m;

    public static bool CanDispatch(string environment, WalletWithdrawalProcessingOptions settings,
        BlaaizOptions blaaiz) =>
        BaseAllowed(environment, settings, blaaiz) && settings.DispatchEnabled;

    public static bool CanRecover(string environment, WalletWithdrawalProcessingOptions settings,
        BlaaizOptions blaaiz) =>
        BaseAllowed(environment, settings, blaaiz) && settings.RecoveryEnabled;

    private static bool BaseAllowed(string environment, WalletWithdrawalProcessingOptions settings,
        BlaaizOptions blaaiz) =>
        string.Equals(environment, Environments.Development, StringComparison.Ordinal) &&
        settings.Enabled && blaaiz.IsEnabled &&
        HasExactSandboxAllowlist(settings) &&
        Uri.TryCreate(blaaiz.BaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("api-dev.blaaiz.com", StringComparison.OrdinalIgnoreCase);

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
        if (row.PaymentMethod == PaymentMethod.BankTransfer &&
            (string.IsNullOrWhiteSpace(unprotectedAccountNumber) ||
             unprotectedAccountNumber.Length != 10 || !unprotectedAccountNumber.All(char.IsAsciiDigit)))
            throw new InvalidOperationException("A verified NGN bank-account number is required.");

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
            AccountName: row.VerifiedAccountName, AccountNumber: unprotectedAccountNumber,
            RoutingNumber: null, SortCode: null, Iban: null, SwiftBic: null,
            Note: "KorridorX wallet withdrawal " + row.Reference,
            FromAmount: row.TotalWalletDebit, MerchantReference: row.Reference);
    }
}
