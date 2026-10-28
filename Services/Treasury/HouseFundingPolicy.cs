using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;

namespace KorridorX.Services.Treasury;

public static class HouseFundingPolicy
{
    public static void AddAccount(HouseFundingPositionDto position, FinancialAccount account)
    {
        // Settled includes available and held funds. Never subtract holds again.
        var positiveSettled = Math.Max(0m, account.SettledBalance);
        if (account.AccountType == FinancialAccountType.Customer)
        {
            position.CustomerLiabilities += positiveSettled;
            position.CustomerHeldIncluded += Math.Max(0m, account.HeldBalance);
            if (account.OwnerType is not (FinancialAccountOwnerType.User or FinancialAccountOwnerType.Business or FinancialAccountOwnerType.BusinessCustomer))
                Warn(position, "A customer account has an unclassified owner. Reconcile it before funding.");
        }
        else if ((account.OwnerType is FinancialAccountOwnerType.Platform or FinancialAccountOwnerType.Treasury) &&
                 (account.AccountType is FinancialAccountType.House or FinancialAccountType.Treasury or FinancialAccountType.Revenue))
        {
            position.RecordedCompanyFunds += positiveSettled;
            if (account.AccountType == FinancialAccountType.House)
            {
                position.RecordedHouseFunds += positiveSettled;
                if (!account.IsDeleted && account.Status == FinancialAccountStatus.Active)
                    position.HouseAccounts.Add(new(account.Id, account.AccountCode, account.AvailableBalance, account.HeldBalance));
            }
        }
        else
        {
            // Existing internal balances are allocations of the same provider cash.
            // They limit a new credit; they do not change Blaaiz minus customer balances.
            position.OtherAllocatedFunds += positiveSettled;
        }
        if (account.AccountType != FinancialAccountType.ProviderClearing &&
            (account.SettledBalance < 0m || account.AvailableBalance < 0m || account.HeldBalance < 0m ||
             account.SettledBalance != account.AvailableBalance + account.HeldBalance))
            Warn(position, "A ledger balance needs reconciliation before company funding can be posted.");
        if (account.IsDeleted && positiveSettled > 0m)
            Warn(position, "A deleted account still has a balance. Its funds remain included for reconciliation.");
    }
    public static void ValidateAmount(HouseFundingPositionDto position, decimal amount)
    {
        if (position.Warnings.Count > 0) throw new InvalidOperationException(string.Join(" ", position.Warnings));
        if (amount <= 0m || decimal.Round(amount, Math.Clamp(position.DecimalPlaces, 0, 18)) != amount)
            throw new InvalidOperationException("Enter a positive amount using the currency's configured precision.");
        if (amount > position.FundingLimit)
            throw new InvalidOperationException("The amount exceeds the house position remaining after existing internal allocations.");
    }
    public static void StageFunding(FinancialAccount house, decimal amount,
        LedgerTransaction ledger, Guid actor, DateTime now)
    {
        if (amount <= 0m || house.AccountType != FinancialAccountType.House || house.IsDeleted ||
            house.Status != FinancialAccountStatus.Active ||
            house.OwnerType is not (FinancialAccountOwnerType.Treasury or FinancialAccountOwnerType.Platform) ||
            ledger.Amount != amount || ledger.AssetCode != house.AssetCode || actor == Guid.Empty)
            throw new InvalidOperationException("A reviewed funding amount and active company house account are required.");
        house.SettledBalance += amount;
        house.AvailableBalance += amount;
        house.LastUpdatedAt = now;
        house.LastUpdatedByUserId = actor;
        // Same external-to-available ledger convention as customer bank funding.
        ledger.Postings.Add(new LedgerPosting { FinancialAccount = house, FinancialAccountId = house.Id,
            BalanceBucket = LedgerBalanceBucket.External, Side = LedgerPostingSide.Debit, Amount = amount });
        ledger.Postings.Add(new LedgerPosting { FinancialAccount = house, FinancialAccountId = house.Id,
            BalanceBucket = LedgerBalanceBucket.Available, Side = LedgerPostingSide.Credit, Amount = amount,
            AccountBalanceAfter = house.AvailableBalance });
    }
    public static void Warn(HouseFundingPositionDto position, string message)
    { if (!position.Warnings.Contains(message)) position.Warnings.Add(message); }
}
