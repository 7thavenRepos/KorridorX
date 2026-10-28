using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.Treasury;
using Microsoft.AspNetCore.Authorization;

namespace KorridorX.Tests;

public sealed class HouseFundingPolicyTests
{
    [Fact]
    public void Customer_holds_are_in_settled_and_are_not_subtracted_twice()
    {
        var p = Position(150m);
        HouseFundingPolicy.AddAccount(p, Account(FinancialAccountType.Customer, FinancialAccountOwnerType.User, 100m, 70m, 30m));
        Assert.Equal(100m, p.CustomerLiabilities);
        Assert.Equal(30m, p.CustomerHeldIncluded);
        Assert.Equal(50m, p.NetCompanyFunds);
        Assert.Equal(50m, p.FundingLimit);
    }
    [Fact]
    public void Personal_business_and_embedded_customer_accounts_all_remain_customer_liabilities()
    {
        var p = Position(1000m);
        foreach (var owner in new[] { FinancialAccountOwnerType.User, FinancialAccountOwnerType.Business, FinancialAccountOwnerType.BusinessCustomer })
            HouseFundingPolicy.AddAccount(p, Account(FinancialAccountType.Customer, owner, 100m, 100m, 0m));
        Assert.Equal(300m, p.CustomerLiabilities);
        Assert.Equal(700m, p.FundingLimit);
    }
    [Fact]
    public void Internal_allocations_limit_new_credit_without_reducing_the_provider_minus_customer_house_position()
    {
        var p = Position(200m);
        HouseFundingPolicy.AddAccount(p, Account(FinancialAccountType.Customer, FinancialAccountOwnerType.User, 100m, 100m, 0m));
        HouseFundingPolicy.AddAccount(p, Account(FinancialAccountType.Suspense, FinancialAccountOwnerType.Treasury, 20m, 20m, 0m));
        HouseFundingPolicy.AddAccount(p, Account(FinancialAccountType.House, FinancialAccountOwnerType.Treasury, 10m, 10m, 0m));
        HouseFundingPolicy.AddAccount(p, Account(FinancialAccountType.Revenue, FinancialAccountOwnerType.Platform, 5m, 5m, 0m));
        Assert.Equal(100m, p.TotalLiabilities);
        Assert.Equal(100m, p.NetCompanyFunds);
        Assert.Equal(15m, p.RecordedCompanyFunds);
        Assert.Equal(20m, p.OtherAllocatedFunds);
        Assert.Equal(35m, p.TotalRecordedAllocations);
        Assert.Equal(65m, p.FundingLimit);
    }
    [Theory]
    [InlineData("NGN", 519500, 20000, 0, 499500)]
    [InlineData("CAD", 518.46, 2.46, 16, 500)]
    public void House_position_uses_current_provider_cash_minus_available_customer_funds_and_holds(
        string asset, double cash, double available, double held, double expected)
    {
        var p = new HouseFundingPositionDto { AssetCode = asset, ProviderBalance = (decimal)cash, DecimalPlaces = 2 };
        var customer = Account(FinancialAccountType.Customer, FinancialAccountOwnerType.User,
            (decimal)available + (decimal)held, (decimal)available, (decimal)held);
        customer.AssetCode = asset;
        HouseFundingPolicy.AddAccount(p, customer);
        Assert.Equal((decimal)expected, p.NetCompanyFunds);
        Assert.Equal((decimal)expected, p.FundingLimit);
        HouseFundingPolicy.ValidateAmount(p, (decimal)expected);
    }
    [Fact]
    public void Negative_net_position_does_not_create_spendable_house_funds()
    {
        var p = Position(50m);
        p.CustomerLiabilities = 100m;
        Assert.Equal(-50m, p.NetCompanyFunds);
        Assert.Equal(0m, p.FundingLimit);
        Assert.Throws<InvalidOperationException>(() => HouseFundingPolicy.ValidateAmount(p, 1m));
    }
    [Fact]
    public void Invalid_customer_balance_cannot_increase_the_funding_limit()
    {
        var p = Position(100m);
        HouseFundingPolicy.AddAccount(p, Account(FinancialAccountType.Customer, FinancialAccountOwnerType.User, -50m, -50m, 0m));
        Assert.Equal(0m, p.CustomerLiabilities);
        Assert.NotEmpty(p.Warnings);
        Assert.Equal(0m, p.FundingLimit);
    }
    [Fact]
    public void Funding_uses_currency_precision_and_cannot_exceed_company_surplus()
    {
        var p = Position(50m);
        Assert.Throws<InvalidOperationException>(() => HouseFundingPolicy.ValidateAmount(p, 0.001m));
        Assert.Throws<InvalidOperationException>(() => HouseFundingPolicy.ValidateAmount(p, 50.01m));
        HouseFundingPolicy.ValidateAmount(p, 50m);
    }
    [Fact]
    public void Company_funding_has_balanced_external_and_available_legs_and_preserves_holds()
    {
        var house = Account(FinancialAccountType.House, FinancialAccountOwnerType.Treasury, 20m, 15m, 5m);
        var ledger = new LedgerTransaction { AssetCode = "NGN", Amount = 10m, Type = LedgerTransactionType.FundingCredit };
        HouseFundingPolicy.StageFunding(house, 10m, ledger, Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(30m, house.SettledBalance);
        Assert.Equal(25m, house.AvailableBalance);
        Assert.Equal(5m, house.HeldBalance);
        Assert.Equal(ledger.Postings.Where(x => x.Side == LedgerPostingSide.Debit).Sum(x => x.Amount),
            ledger.Postings.Where(x => x.Side == LedgerPostingSide.Credit).Sum(x => x.Amount));
        Assert.Contains(ledger.Postings, x => x.Side == LedgerPostingSide.Debit && x.BalanceBucket == LedgerBalanceBucket.External);
    }
    [Fact]
    public void Customer_wallet_cannot_be_substituted_for_a_company_house_target()
    {
        var customer = Account(FinancialAccountType.Customer, FinancialAccountOwnerType.User, 20m, 20m, 0m);
        Assert.Throws<InvalidOperationException>(() => HouseFundingPolicy.StageFunding(customer, 10m,
            new LedgerTransaction { AssetCode = "NGN", Amount = 10m }, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Equal(20m, customer.SettledBalance);
    }
    [Theory]
    [InlineData("Preview")]
    [InlineData("Confirm")]
    public void Funding_actions_require_admin_approval_roles(string method)
    {
        var authorization = typeof(AdminHouseFundingController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("Admin,SuperAdmin", authorization!.Roles);
    }
    private static HouseFundingPositionDto Position(decimal cash) => new() { AssetCode = "NGN", ProviderBalance = cash, DecimalPlaces = 2 };
    private static FinancialAccount Account(FinancialAccountType type, FinancialAccountOwnerType owner, decimal settled, decimal available, decimal held) =>
        new() { AssetCode = "NGN", AccountType = type, OwnerType = owner, Status = FinancialAccountStatus.Active,
            SettledBalance = settled, AvailableBalance = available, HeldBalance = held };
}
