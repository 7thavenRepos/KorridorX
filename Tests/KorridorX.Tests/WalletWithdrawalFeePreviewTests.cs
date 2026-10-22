using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Providers;
using KorridorX.Services.WalletWithdrawals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalFeePreviewTests
{
    [Theory]
    [InlineData(PaymentMethod.BankTransfer, "BANK_TRANSFER")]
    [InlineData(PaymentMethod.Interac, "INTERAC")]
    public void Correct_payout_rail_fee_is_used_without_double_counting_the_provider_base_fee(
        PaymentMethod method, string code)
    {
        var response = Reply(
            Quote("CARD", 100m, 70m, 30m),
            Quote(code, 100m, 96.5m, 3.5m),
            Quote("MOBILE_MONEY", 100m, 95m, 5m));

        var amounts = WalletWithdrawalFeePolicy.Evaluate(response, method, 100m, 2);

        Assert.Equal(3.5m, amounts.ProviderFee);
        Assert.Equal(96.5m, amounts.RecipientAmount);
    }

    [Fact]
    public void Exact_zero_provider_fee_is_valid_only_when_the_provider_returns_every_required_zero_field()
    {
        var zero = Quote("BANK_TRANSFER", 100m, 100m, 0m);
        zero.MethodFee = 0m;
        zero.BaseFee = 0m;
        Assert.Equal(0m, WalletWithdrawalFeePolicy.Evaluate(Reply(zero), PaymentMethod.BankTransfer, 100m, 2).ProviderFee);
        zero.MethodFee = null;
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalFeePolicy.Evaluate(
            Reply(zero), PaymentMethod.BankTransfer, 100m, 2));
    }

    [Theory]
    [InlineData("no-method")]
    [InlineData("duplicate")]
    [InlineData("missing-total-fees")]
    [InlineData("missing-recipient")]
    [InlineData("wrong-send")]
    [InlineData("fee-mismatch")]
    [InlineData("bad-recipient")]
    [InlineData("bad-net")]
    [InlineData("cross-currency-rate")]
    [InlineData("wrong-amount-direction")]
    [InlineData("fractional-minor-unit")]
    [InlineData("fee-exceeds-amount")]
    public void Malformed_or_ambiguous_provider_quote_fails_closed(string caseName)
    {
        var quote = Quote("BANK_TRANSFER", 100m, 96.5m, 3.5m);
        var response = Reply(quote);
        switch (caseName)
        {
            case "no-method": quote.Method = "CARD"; break;
            case "duplicate": response.PayoutFees!.Add(Quote("bank_transfer", 100m, 96.5m, 3.5m)); break;
            case "missing-total-fees": quote.TotalFees = null; break;
            case "missing-recipient": quote.RecipientGets = null; break;
            case "wrong-send": quote.YouSend = 99m; break;
            case "fee-mismatch": quote.MethodFee = 2.1m; break;
            case "bad-recipient": quote.RecipientGets = 96.49m; break;
            case "bad-net": quote.NetAmountToSend = 96m; break;
            case "cross-currency-rate": quote.Rate = 1.1m; break;
            case "wrong-amount-direction": quote.AmountIsYouSend = false; break;
            case "fractional-minor-unit": quote.TotalFees = 3.501m; break;
            case "fee-exceeds-amount": quote.TotalFees = 101m; break;
        }

        var error = Assert.Throws<InvalidOperationException>(() =>
            WalletWithdrawalFeePolicy.Evaluate(response, PaymentMethod.BankTransfer, 100m, 2));
        Assert.Contains("No funds have been reserved or moved", error.Message);
    }

    [Theory]
    [InlineData(0, 100, 2)]
    [InlineData(-1, 100, 2)]
    [InlineData(100.01, 100, 2)]
    [InlineData(10.001, 100, 2)]
    [InlineData(10.5, 100, 0)]
    [InlineData(1, 100, 19)]
    public void Invalid_wallet_amounts_or_currency_precision_are_rejected(decimal amount, decimal available, int places) =>
        Assert.Throws<InvalidOperationException>(() => WalletWithdrawalFeePolicy.ValidateAmount(amount, available, places));

    [Fact]
    public async Task Blaaiz_fee_contract_uses_same_currency_customer_specific_from_amount_only_and_never_calls_payout()
    {
        const string providerJson = """
        {"our_fee":1.5,"payout_fees":[
          {"method":"BANK_TRANSFER","fee":2,"our_fee":1.5,"total_fees":3.5,
           "you_send":100,"total_amount_to_send":96.5,"recipient_gets":96.5,
           "rate":1,"amountIsRecipientGets":false,"amountIsYouSend":true}]}
        """;
        using var handler = new QuoteHandler(providerJson);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://provider.example.test") };
        var client = new BlaaizApiClient(http, new Token(), new Audit());
        var customerId = Guid.NewGuid().ToString("D");

        var reply = await client.GetFeeBreakdownAsync(new BlaaizFeeBreakdownRequest
        {
            FromCurrencyId = "NGN", ToCurrencyId = "NGN", FromAmount = 100m, CustomerId = customerId
        });

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/external/fees/breakdown", handler.Path);
        using var payload = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("NGN", payload.RootElement.GetProperty("from_currency_id").GetString());
        Assert.Equal("NGN", payload.RootElement.GetProperty("to_currency_id").GetString());
        Assert.Equal(100m, payload.RootElement.GetProperty("from_amount").GetDecimal());
        Assert.Equal(customerId, payload.RootElement.GetProperty("customer_id").GetString());
        Assert.False(payload.RootElement.TryGetProperty("to_amount", out _));
        Assert.Equal(3.5m, WalletWithdrawalFeePolicy.Evaluate(reply.Data, PaymentMethod.BankTransfer, 100m, 2).ProviderFee);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Mismatched_currency_request_is_rejected_before_any_provider_HTTP_call()
    {
        using var handler = new QuoteHandler("{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://provider.example.test") };
        var client = new BlaaizApiClient(http, new Token(), new Audit());
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetFeeBreakdownAsync(
            new BlaaizFeeBreakdownRequest { FromCurrencyId = "NGN", ToCurrencyId = "CAD", FromAmount = 100m, CustomerId = Guid.NewGuid().ToString() }));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Both_quote_controllers_have_separate_roles_and_expose_only_a_post_preview()
    {
        CheckController<ConsumerFiatWithdrawalFeePreviewController>("Consumer");
        CheckController<BusinessFiatWithdrawalFeePreviewController>("Business");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WalletWithdrawalFeePreviewRequestDto>("{}"));
    }

    private static void CheckController<T>(string role)
    {
        var controller = typeof(T);
        Assert.Equal(role, controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        var actions = controller.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute<HttpPostAttribute>() is not null).ToArray();
        Assert.Equal("Preview", Assert.Single(actions).Name);
    }

    private static BlaaizFeeBreakdownResponse Reply(params BlaaizPayoutFeeBreakdown[] fees) =>
        new() { PayoutFees = fees.ToList() };

    private static BlaaizPayoutFeeBreakdown Quote(string method, decimal send, decimal receive, decimal totalFees) => new()
    {
        Method = method, MethodFee = totalFees - 1.5m, BaseFee = 1.5m, TotalFees = totalFees,
        YouSend = send, RecipientGets = receive, NetAmountToSend = receive, Rate = 1m,
        AmountIsYouSend = true, AmountIsRecipientGets = false
    };

    private sealed class QuoteHandler(string response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Token : IBlaaizTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("The fee endpoint must never use the standard payout token.");
        public Task<string> GetFeeReadAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fee-read-fixture-token");
    }

    private sealed class Audit : IProviderRequestAuditService
    {
        public Task<Guid> StartAsync(ProviderCode providerCode, string endpoint, string httpMethod,
            string? requestHeadersJson, string? requestBodyJson, Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null, Guid? relatedPayoutId = null, CancellationToken ct = default) =>
            Task.FromResult(Guid.NewGuid());
        public Task CompleteAsync(Guid requestLogId, int responseStatusCode, string? responseBodyJson,
            long durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public Task FailAsync(Guid requestLogId, int? responseStatusCode, string? responseBodyJson,
            string errorMessage, long durationMs, CancellationToken ct = default) => Task.CompletedTask;
    }
}
