using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Providers.Remittance;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.PaymentChannels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KorridorX.Tests;

public sealed class PaymentChannelTests
{
    [Fact] public void Capability_identity_is_unique_and_blaaiz_only()
    {
        var rows = BlaaizChannelCapabilities.All;
        Assert.All(rows, x => Assert.Equal("BLAAIZ", x.ProviderCode));
        Assert.Equal(rows.Count, rows.Select(x => (x.Code, x.AssetCode, x.Direction)).Distinct().Count());
    }
    [Theory][InlineData("NGN", true)][InlineData("USD", false)][InlineData("GBP", false)][InlineData("EUR", false)]
    public void Bank_account_audience_is_provider_specific(string asset, bool business)
    {
        var cap = BlaaizChannelCapabilities.Find("virtual-account", asset, PaymentChannelDirection.Deposit)!;
        Assert.True(cap.Implemented); Assert.True(cap.IndividualSupported); Assert.Equal(business, cap.BusinessSupported);
    }
    [Theory][InlineData("NGN")][InlineData("USD")]
    public void Raw_card_flow_is_not_exposed_to_business_web(string asset) =>
        Assert.False(BlaaizChannelCapabilities.Find("card", asset, PaymentChannelDirection.Deposit)!.BusinessSupported);
    // These foreign-currency rails became executable in W8. A capability being
    // implemented does not by itself activate a payment channel: Admin must
    // still enable a verified provider wallet, asset and approved payout route.
    [Theory]
    [InlineData("USD", "ach-payout", PaymentMethod.Ach)]
    [InlineData("USD", "wire-payout", PaymentMethod.Wire)]
    [InlineData("EUR", "bank-payout", PaymentMethod.BankTransfer)]
    [InlineData("GBP", "bank-payout", PaymentMethod.BankTransfer)]
    public void Foreign_payout_capabilities_are_implemented_for_both_audiences(string asset, string code, PaymentMethod method)
    {
        var capability = BlaaizChannelCapabilities.Find(code, asset, PaymentChannelDirection.Withdrawal);
        Assert.NotNull(capability);
        Assert.True(capability!.Implemented);
        Assert.Equal(method, capability.PaymentMethod);
        Assert.True(capability.IndividualSupported);
        Assert.True(capability.BusinessSupported);
        Assert.True(string.IsNullOrWhiteSpace(capability.BlockedReason));
    }

    [Theory]
    [InlineData("USD", "interac-payout")]
    [InlineData("CAD", "bank-payout")]
    [InlineData("EUR", "ach-payout")]
    [InlineData("GBP", "wire-payout")]
    public void Unsupported_currency_and_payout_method_combinations_remain_unavailable(string asset, string code) =>
        Assert.Null(BlaaizChannelCapabilities.Find(code, asset, PaymentChannelDirection.Withdrawal));
    [Fact] public void Draft_does_not_opt_in_and_route_snapshot_is_independent()
    { Assert.False(new PaymentChannel().HasBeenActivated); Assert.Equal(PaymentChannelStatus.Draft, new PaymentChannel().Status); Assert.Null(new PaymentChannelSelection().Amount); }
    [Theory][InlineData("*", "NG", true)][InlineData("CA", "CA", true)][InlineData("CA", "NG", false)]
    public void Explicit_country_scope_is_not_currency_inference(string config, string country, bool expected) =>
        Assert.Equal(expected, BlaaizChannelCapabilities.MatchesCountry(config, country));
    [Theory][InlineData(1, 10, 0, false)][InlineData(1, 10, 1, true)][InlineData(1, 10, 10, true)][InlineData(1, 10, 11, false)]
    public void Amount_boundaries_are_inclusive(decimal min, decimal max, decimal amount, bool expected) =>
        Assert.Equal(expected, BlaaizChannelCapabilities.MatchesAmount(min, max, amount));
    [Fact] public void Unlimited_maximum_does_not_require_a_fake_limit() => Assert.True(BlaaizChannelCapabilities.MatchesAmount(0, null, 123m));
    [Fact] public void Admin_management_is_superadmin_only_and_consumer_accounts_are_protected()
    {
        Assert.Equal("SuperAdmin", typeof(AdminPaymentChannelsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("Consumer", typeof(WalletDepositAccountsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.NotNull(typeof(BusinessPaymentChannelsController).GetCustomAttribute<AuthorizeAttribute>());
        foreach (var type in new[] { typeof(AdminPaymentChannelsController), typeof(WalletDepositAccountsController), typeof(BusinessPaymentChannelsController) })
        { Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>()); Assert.All(type.GetMethods(), x => Assert.Empty(x.GetCustomAttributes<AllowAnonymousAttribute>())); }
        Assert.Equal("api/admin/payment-channels", typeof(AdminPaymentChannelsController).GetCustomAttribute<RouteAttribute>()!.Template);
    }
    [Fact] public void Database_enforces_immutable_operation_and_receiving_account_uniqueness()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Database=not_connected;Username=test").Options);
        var route = db.Model.FindEntityType(typeof(PaymentChannelSelection))!;
        Assert.Contains(route.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "OperationType", "OperationId" }));
        var account = db.Model.FindEntityType(typeof(WalletDepositAccount))!;
        Assert.Contains(account.GetIndexes(), x => x.IsUnique && x.GetDatabaseName() == "UX_WalletDepositAccount_ProviderAccount");
        Assert.True(db.Model.FindEntityType(typeof(PaymentChannel))!.FindProperty("Revision")!.IsConcurrencyToken);
    }
    [Fact] public void Pending_accounts_never_leak_preliminary_bank_details()
    {
        var account = Account(); account.Status = "Pending"; account.AccountNumber = "SHOULD_NOT_APPEAR";
        var dto = WalletDepositAccountService.ToDto(account); Assert.False(dto.Instructions.IsReady); Assert.Empty(dto.Instructions.Fields);
        Assert.DoesNotContain("SHOULD_NOT_APPEAR", JsonSerializer.Serialize(dto));
    }
    [Fact] public void Successful_account_creation_is_not_a_wallet_credit()
    { var dto = WalletDepositAccountService.ToDto(Account()); Assert.True(dto.Instructions.IsReady); Assert.Contains("confirmed deposit", dto.Instructions.Message); }
    [Theory][InlineData("customer")][InlineData("wallet")][InlineData("currency")][InlineData("account")]
    public void Ready_event_identity_must_match_saved_owner(string changed)
    {
        var state = new BlaaizVirtualAccountState(changed == "account" ? "wrong" : "account", changed == "customer" ? "wrong" : "customer",
            changed == "wallet" ? "wrong" : "wallet", changed == "currency" ? "USD" : "NGN", ProviderAccountMappingStatus.Active,
            "0123456789", "Test", "Bank", null, null, null, null, null);
        Assert.Throws<InvalidOperationException>(() => WalletDepositAccountService.ApplyState(Account(), state));
    }
    [Fact] public void Valid_verified_deposit_passes_identity_and_amount_checks() => WalletDepositAccountService.ValidateVerifiedDeposit(Account(), Request(), Verified());
    [Theory][InlineData("amount")][InlineData("currency")][InlineData("status")][InlineData("type")][InlineData("transaction")][InlineData("wallet")][InlineData("customer")][InlineData("account")]
    public void Unverified_mismatched_deposit_is_rejected(string changed)
    {
        var verified = Verified() with {
            Amount = changed == "amount" ? 101m : 100m, CurrencyCode = changed == "currency" ? "USD" : "NGN",
            ProviderStatus = changed == "status" ? "PENDING" : "SUCCESSFUL", TransactionType = changed == "type" ? "payout" : "collection",
            ProviderTransactionId = changed == "transaction" ? "other" : "deposit",
            RawResponseJson = JsonSerializer.Serialize(new { data = new { wallet_id = changed == "wallet" ? "wrong" : "wallet",
                customer_id = changed == "customer" ? "wrong" : "customer", virtual_account_id = changed == "account" ? "wrong" : "account" } }) };
        Assert.Throws<InvalidOperationException>(() => WalletDepositAccountService.ValidateVerifiedDeposit(Account(), Request(), verified));
    }
    [Fact] public void Migration_snapshot_matches_the_current_model_without_connecting_to_Postgres()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Database=not_connected;Username=test").Options);
        var previous = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(previous.GetRelationalModel(), current.GetRelationalModel());
        Assert.Empty(operations);
    }
    private static WalletDepositAccount Account() => new() { FinancialAccountId = Guid.NewGuid(), PaymentChannelId = Guid.NewGuid(),
        ProviderAccountId = "account", ProviderCustomerId = "customer", ProviderWalletId = "wallet", AssetCode = "NGN", Status = "Active", AccountNumber = "0123456789" };
    private static EmbeddedInboundCollectionRequest Request() => new("deposit", null, "SUCCESSFUL", "NGN", 100m, null, null, null,
        "account", "customer", null, "0123456789", "{}", DateTime.UtcNow);
    private static RemittanceTransactionStatusResult Verified() => new("deposit", null, "collection", "SUCCESSFUL", "NGN", 100m, null, null, "{}", Guid.NewGuid());
}
