using KorridorX.Models.Enums;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedFinanceContextAccessor { EmbeddedFinancePrincipal GetRequiredPrincipal(); }

public sealed record EmbeddedFinancePrincipal(Guid ApiApplicationId, Guid ApiCredentialId, Guid BusinessProfileId, EmbeddedFinanceScope Scopes, string KeyId)
{
    public bool HasScope(EmbeddedFinanceScope scope) => (Scopes & scope) == scope;
}
