using KorridorX.Models.Enums;

namespace KorridorX.Services.Security;

public static class RegistrationSecurityPolicy
{
    public static string ResolvePublicRole(UserType userType) => userType switch
    {
        UserType.Consumer => "Consumer",
        UserType.Business => "Business",
        UserType.Admin => throw new InvalidOperationException(
            "Administrative accounts cannot be created through public registration."),
        _ => throw new InvalidOperationException("A valid public user type is required.")
    };
}
