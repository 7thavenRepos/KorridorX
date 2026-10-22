using System.Reflection;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Services.WalletWithdrawals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalInitiationPolicyTests
{
    [Fact]
    public void Idempotency_key_is_not_saved_in_plaintext_and_is_stable()
    {
        const string key = "unique_withdrawal_123456";
        var hash = WalletWithdrawalInitiationPolicy.HashIdempotencyKey(key);
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(key, hash);
        Assert.Equal(hash, WalletWithdrawalInitiationPolicy.HashIdempotencyKey(key));
        Assert.NotEqual(hash, WalletWithdrawalInitiationPolicy.HashIdempotencyKey(key + "7"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("untrusted key")]
    [InlineData("abc/def/ghi/jkl")]
    [InlineData("abc\r\nInjected:Header")]
    public void Missing_short_or_unsafe_idempotency_key_fails_closed(string? key) =>
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalInitiationPolicy.HashIdempotencyKey(key));

    [Fact]
    public void Idempotency_key_rejects_overlong_headers()
    {
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalInitiationPolicy.HashIdempotencyKey(new string('a', 129)));
        Assert.Equal(64, WalletWithdrawalInitiationPolicy.HashIdempotencyKey(new string('a', 128)).Length);
    }

    [Fact]
    public void Request_fingerprint_is_canonical_for_equivalent_money_amounts()
    {
        var walletId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var baseHash = WalletWithdrawalInitiationPolicy.Fingerprint(walletId, destinationId, channelId, 100.00m, false);
        Assert.Equal(baseHash, WalletWithdrawalInitiationPolicy.Fingerprint(walletId, destinationId, channelId, 100m, false));
        Assert.NotEqual(baseHash, WalletWithdrawalInitiationPolicy.Fingerprint(walletId, destinationId, channelId, 101m, false));
        Assert.NotEqual(baseHash, WalletWithdrawalInitiationPolicy.Fingerprint(walletId, Guid.NewGuid(), channelId, 100m, false));
        Assert.NotEqual(baseHash, WalletWithdrawalInitiationPolicy.Fingerprint(walletId, destinationId, Guid.NewGuid(), 100m, false));
        Assert.NotEqual(baseHash, WalletWithdrawalInitiationPolicy.Fingerprint(walletId, destinationId, channelId, 100m, true));
    }

    [Fact]
    public void Same_key_with_changed_request_is_rejected()
    {
        var row = new FiatWalletWithdrawal { RequestFingerprint = "original" };
        WalletWithdrawalInitiationPolicy.EnsureSame(row, "original");
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalInitiationPolicy.EnsureSame(row, "different"));
    }

    [Fact]
    public void Fresh_inclusive_provider_quote_with_zero_platform_fee_reconciles()
    {
        var now = DateTime.UtcNow;
        var quote = Quote(now);
        WalletWithdrawalInitiationPolicy.EnsureFreshQuote(quote, 100m, now);
        Assert.Equal(100m, quote.TotalWalletDebit);
        Assert.Equal(96.5m, quote.ExpectedRecipientAmount);
    }

    [Fact]
    public void Stale_future_or_opted_out_quote_is_rejected()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalInitiationPolicy.EnsureFreshQuote(Quote(now.AddSeconds(-31)), 100m, now));
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalInitiationPolicy.EnsureFreshQuote(Quote(now.AddSeconds(3)), 100m, now));
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalInitiationPolicy.EnsureFreshQuote(Quote(now) with { RequiresFreshQuoteBeforeExecution = false }, 100m, now));
    }

    [Theory]
    [InlineData("fee")]
    [InlineData("net")]
    [InlineData("debit")]
    [InlineData("platform")]
    [InlineData("policy")]
    public void Invalid_price_or_hidden_platform_fee_fails_closed(string variant)
    {
        var now = DateTime.UtcNow;
        var quote = Quote(now);
        quote = variant switch
        {
            "fee" => quote with { ProviderFee = -0.01m },
            "net" => quote with { ExpectedRecipientAmount = 96.49m },
            "debit" => quote with { TotalWalletDebit = 103.5m },
            "platform" => quote with { KorridorXFee = 1m },
            _ => quote with { KorridorXFeePolicy = "UNKNOWN_FEE_RULE" }
        };
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalInitiationPolicy.EnsureFreshQuote(quote, 100m, now));
    }

    [Fact]
    public void Funds_cannot_be_reserved_through_configuration_by_default()
    {
        Assert.False(new WalletWithdrawalProcessingOptions().Enabled);
        Assert.Equal("WalletWithdrawalProcessing", WalletWithdrawalProcessingOptions.SectionName);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Staging", true)]
    [InlineData("Production", true)]
    public async Task Processing_gate_rejects_all_disabled_or_non_development_requests_before_any_dependency_is_used(
        string environment, bool enabled)
    {
        // If the gate is not the first step this test fails with a null
        // dependency, protecting against accidentally live reservations.
        var service = new WalletWithdrawalInitiationService(
            db: null!, contexts: null!, previews: null!, reservations: null!,
            pins: null!, businessAccess: null!, complianceLimits: null!,
            notifications: null!, audit: null!,
            options: Options.Create(new WalletWithdrawalProcessingOptions { Enabled = enabled }),
            environment: new HostingEnvironment { EnvironmentName = environment });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InitiateAsync(Guid.NewGuid(), Guid.NewGuid(), business: false,
                new InitiateWalletWithdrawalRequestDto(), "safe_key_123456"));
        Assert.Contains("not enabled", error.Message);
    }

    [Fact]
    public void Initiation_contract_has_no_provider_dispatch_or_funds_release_endpoint()
    {
        foreach (var (controller, role) in new[]
        {
            (typeof(ConsumerFiatWithdrawalInitiationController), "Consumer"),
            (typeof(BusinessFiatWithdrawalInitiationController), "Business")
        })
        {
            Assert.Equal(role, controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Equal("Initiate", Assert.Single(actions, x => x.GetCustomAttribute<HttpPostAttribute>() is not null).Name);
            Assert.Equal(2, actions.Count(x => x.GetCustomAttribute<HttpGetAttribute>() is not null));
            Assert.DoesNotContain(actions, x => x.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Settle", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Release", StringComparison.OrdinalIgnoreCase));
        }
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InitiateWalletWithdrawalRequestDto>("{}"));
    }

    [Fact]
    public void Withdrawal_model_has_unique_owner_idempotency_constraint_and_no_plaintext_account_number()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=withdrawal_metadata_only;Username=unused;Password=unused").Options);
        var model = db.Model.FindEntityType(typeof(FiatWalletWithdrawal));
        Assert.NotNull(model);
        Assert.Contains(model!.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            { nameof(FiatWalletWithdrawal.OwnerType), nameof(FiatWalletWithdrawal.OwnerId), nameof(FiatWalletWithdrawal.IdempotencyKeyHash) }));
        Assert.NotNull(model.FindProperty(nameof(FiatWalletWithdrawal.ProtectedAccountNumber)));
        Assert.Null(model.FindProperty("AccountNumber"));
        Assert.Equal(FiatWalletWithdrawalStatus.AwaitingExecution, new FiatWalletWithdrawal().Status);
    }

    private static WalletWithdrawalFeePreviewDto Quote(DateTime timestamp) => new(
        WalletId: Guid.NewGuid(), ChannelId: Guid.NewGuid(), DestinationId: Guid.NewGuid(),
        WalletCurrency: "NGN", CurrencyDecimalPlaces: 2, WithdrawalAmount: 100m,
        ProviderFee: 3.5m, KorridorXFee: 0m, KorridorXFeePolicy: "NOT_CONFIGURED",
        TotalWalletDebit: 100m, ExpectedRecipientAmount: 96.5m, AvailableBalance: 200m,
        Destination: new WalletWithdrawalDestinationDto(Guid.NewGuid(), Guid.NewGuid(), "NGN", "NG",
            PaymentMethod.BankTransfer, "Bank transfer", "Example Bank", "•••• 1234", "Owner", null,
            true, timestamp), QuotedAtUtc: timestamp, RequiresFreshQuoteBeforeExecution: true);
}
