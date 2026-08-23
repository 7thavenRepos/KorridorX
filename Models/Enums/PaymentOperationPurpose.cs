namespace KorridorX.Models.Enums;

public enum PaymentOperationPurpose
{
    Remittance = 1,
    AccountFunding = 2,
    Withdrawal = 3,
    BusinessCollection = 4,
    MarketplaceSettlement = 5,
    InstantSettlement = 6,
    Treasury = 7,
    Other = 99
}