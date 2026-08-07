using KorridorX.Models.Enums;
using KorridorX.Services.Security;

namespace KorridorX.Tests;

public class RegistrationSecurityPolicyTests
{
    [Theory]
    [InlineData(UserType.Consumer, "Consumer")]
    [InlineData(UserType.Business, "Business")]
    public void ResolvePublicRole_AllowsSupportedPublicAccountTypes(
        UserType userType,
        string expectedRole)
    {
        Assert.Equal(expectedRole, RegistrationSecurityPolicy.ResolvePublicRole(userType));
    }

    [Fact]
    public void ResolvePublicRole_RejectsAdministrativeRegistration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            RegistrationSecurityPolicy.ResolvePublicRole(UserType.Admin));

        Assert.Contains("cannot be created through public registration", exception.Message);
    }
}
