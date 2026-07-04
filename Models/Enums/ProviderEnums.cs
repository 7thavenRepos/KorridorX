namespace KorridorX.Models.Enums;

public enum ProviderCode
{
    Blaaiz = 1
}

public enum ProviderRequestStatus
{
    Pending = 1,
    Successful = 2,
    Failed = 3
}

public enum WebhookProcessingStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
    Ignored = 5
}