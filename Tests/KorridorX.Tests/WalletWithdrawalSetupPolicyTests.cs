using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Models.WalletWithdrawals;
using Microsoft.EntityFrameworkCore;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Models.Enums;
using KorridorX.Services.WalletWithdrawals;
using Microsoft.AspNetCore.Authorization;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalSetupPolicyTests
{
    [Theory]
    [InlineData("NGN", PaymentMethod.BankTransfer, "NG")]
    [InlineData("CAD", PaymentMethod.Interac, "CA")]
    [InlineData("USD", PaymentMethod.Ach, "US")]
    [InlineData("USD", PaymentMethod.Wire, "US")]
    [InlineData("GBP", PaymentMethod.BankTransfer, "GB")]
    public void Supported_same_currency_fiat_methods_resolve_to_payout_country(
        string asset, PaymentMethod method, string expected) =>
        Assert.Equal(expected, WalletWithdrawalSetupPolicy.DestinationCountry(asset, method));

    [Theory]
    [InlineData("DE")]
    [InlineData("FR")]
    [InlineData("NL")]
    public void Eur_bank_transfer_uses_real_selected_bank_country(string country) =>
        Assert.Equal(country, WalletWithdrawalSetupPolicy.DestinationCountry(
            "EUR", PaymentMethod.BankTransfer, country));

    [Theory]
    [InlineData("NGN", PaymentMethod.Interac)]
    [InlineData("CAD", PaymentMethod.BankTransfer)]
    [InlineData("USD", PaymentMethod.BankTransfer)]
    [InlineData("GBP", PaymentMethod.Wire)]
    [InlineData("EUR", PaymentMethod.Sepa)]
    [InlineData("EUR", PaymentMethod.BankTransfer)]
    [InlineData("NGN", PaymentMethod.DigitalAsset)]
    public void Unsupported_methods_and_missing_eur_country_fail_closed(
        string asset, PaymentMethod method) =>
        Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalSetupPolicy.DestinationCountry(asset, method));

    [Theory]
    [InlineData("D")]
    [InlineData("D1")]
    [InlineData("de")]
    [InlineData("ZZZ")]
    public void Eur_rejects_malformed_bank_country_codes(string country) =>
        Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalSetupPolicy.DestinationCountry("EUR", PaymentMethod.BankTransfer, country));

    [Theory]
    [InlineData("Olukayode Adebiyi", "OLUKAYODE OLUFEMI ADEBIYI")]
    [InlineData("Maple River Limited", "MAPLE RIVER LTD")]
    [InlineData("Jane Smith", "SMITH JANE")]
    public void Additional_middle_names_order_and_company_suffix_do_not_block_same_owner(string expected, string verified) =>
        Assert.True(WalletWithdrawalSetupPolicy.IsOwnAccount(expected, verified));

    [Fact]
    public void One_word_registered_business_can_match_exact_legal_name_without_suffix()
    {
        Assert.True(WalletWithdrawalSetupPolicy.IsOwnAccount("SkrinAd Limited", "SKRINAD LTD", business: true));
        Assert.False(WalletWithdrawalSetupPolicy.IsOwnAccount("SkrinAd Limited", "SKRINED LTD", business: true));
        Assert.False(WalletWithdrawalSetupPolicy.IsOwnAccount("SkrinAd Limited", "SKRINAD LTD"));
    }

    [Theory]
    [InlineData("Olukayode Adebiyi", "JOHN SMITH")]
    [InlineData("Company Ltd", "COMPANY LTD")]
    [InlineData("Jane Smith", "JANE SMITHSON")]
    [InlineData("Jane Smith", "")]
    [InlineData("", "JANE SMITH")]
    public void Third_party_or_ambiguous_name_cannot_be_saved_as_self_withdrawal(string expected, string verified) =>
        Assert.False(WalletWithdrawalSetupPolicy.IsOwnAccount(expected, verified));

    [Fact]
    public void Bank_account_normalization_preserves_leading_zeros()
    {
        Assert.Equal("0036646835", WalletWithdrawalSetupPolicy.NormalizeNgnAccountNumber(" 0036646835 "));
        Assert.Equal("•••• 6835", WalletWithdrawalSetupPolicy.Mask("0036646835"));
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalSetupPolicy.NormalizeNgnAccountNumber("003664683"));
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalSetupPolicy.NormalizeNgnAccountNumber("0036646835\n"));
    }

    [Theory]
    [InlineData("person@example.test", "person@example.test")]
    [InlineData(" PERSON+CAD@Example.Test ", "person+cad@example.test")]
    public void Email_destination_is_canonicalized_to_one_exact_address(string value, string expected) =>
        Assert.Equal(expected, WalletWithdrawalSetupPolicy.NormalizeVerifiedEmail(value));

    [Theory]
    [InlineData("x@example.test,another@example.test")]
    [InlineData("Person <person@example.test>")]
    [InlineData("person@example.test\r\nBcc: attacker@example.test")]
    public void Email_destination_rejects_multiple_or_header_injected_addresses(string value) =>
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalSetupPolicy.NormalizeVerifiedEmail(value));

    [Fact]
    public void Missing_payment_method_cannot_default_to_bank_transfer()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SaveWalletWithdrawalDestinationRequestDto>("{}"));
    }

    [Fact]
    public void Destination_model_is_scoped_to_wallet_owner_and_never_plaintext_bank_number()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=withdrawal_model_only;Username=unused;Password=unused").Options);
        var entity = db.Model.FindEntityType(typeof(FiatWalletWithdrawalDestination));
        Assert.NotNull(entity);
        Assert.NotNull(entity!.FindProperty(nameof(FiatWalletWithdrawalDestination.ProtectedAccountNumber)));
        Assert.NotNull(entity.FindProperty(nameof(FiatWalletWithdrawalDestination.OwnerType)));
        Assert.NotNull(entity.FindProperty(nameof(FiatWalletWithdrawalDestination.OwnerId)));
        Assert.Contains(entity.GetIndexes(), x =>
            x.Properties.Any(y => y.Name == nameof(FiatWalletWithdrawalDestination.FinancialAccountId)) &&
            x.Properties.Any(y => y.Name == nameof(FiatWalletWithdrawalDestination.OwnerId)));
        Assert.Null(entity.FindProperty("AccountNumber"));
    }

    [Fact]
    public void Controllers_have_separate_personal_and_business_security_boundaries()
    {
        Assert.Equal("Consumer", typeof(ConsumerFiatWithdrawalSetupController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("Business", typeof(BusinessFiatWithdrawalSetupController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        foreach (var controller in new[] { typeof(ConsumerFiatWithdrawalSetupController), typeof(BusinessFiatWithdrawalSetupController) })
            Assert.DoesNotContain(controller.GetMethods(), x => x.DeclaringType == controller &&
                (x.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                 x.Name.Contains("Credit", StringComparison.OrdinalIgnoreCase) ||
                 x.Name.Contains("Withdraw", StringComparison.OrdinalIgnoreCase) && x.Name != "Methods"));
    }
}
