namespace KorridorX.Providers.Remittance.Blaaiz.Models;

public sealed record BlaaizApiResult<T>(
    T Data,
    string RawResponseJson,
    Guid RequestLogId);
