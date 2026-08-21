namespace KorridorX.Models.Enums;

[Flags]
public enum EmbeddedFinanceScope : long
{
    None = 0,
    CustomersRead = 1L << 0,
    CustomersWrite = 1L << 1,
    AccountsRead = 1L << 2,
    AccountsWrite = 1L << 3,
    CollectionsRead = 1L << 4,
    CollectionsWrite = 1L << 5,
    PayoutsRead = 1L << 6,
    PayoutsWrite = 1L << 7,
    TransfersRead = 1L << 8,
    TransfersWrite = 1L << 9,
    WebhooksManage = 1L << 10,
    All = CustomersRead | CustomersWrite | AccountsRead | AccountsWrite |
          CollectionsRead | CollectionsWrite | PayoutsRead | PayoutsWrite |
          TransfersRead | TransfersWrite | WebhooksManage
}

public enum ApiApplicationStatus { Active = 1, Suspended = 2, Disabled = 3 }
public enum ApiCredentialStatus { Active = 1, Revoked = 2, Expired = 3 }
public enum BusinessCustomerStatus { Active = 1, Suspended = 2, Closed = 3 }
public enum CollectionAccountStatus { Pending = 1, Active = 2, Suspended = 3, Closed = 4 }
public enum ProviderAccountMappingStatus { Pending = 1, Active = 2, Failed = 3, Disabled = 4 }


public enum BusinessWebhookEndpointStatus
{
    Active = 1,
    Disabled = 2
}

public enum BusinessWebhookDeliveryStatus
{
    Pending = 1,
    Processing = 2,
    Retry = 3,
    Delivered = 4,
    DeadLetter = 5
}
