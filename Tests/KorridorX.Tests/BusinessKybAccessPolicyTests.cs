using KorridorX.Controllers;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.Compliance;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;

namespace KorridorX.Tests;

public sealed class BusinessKybAccessPolicyTests
{
    [Theory]
    [InlineData(BusinessUserRole.Owner, true, true, true)]
    [InlineData(BusinessUserRole.Admin, false, true, true)]
    [InlineData(BusinessUserRole.Compliance, false, true, false)]
    [InlineData(BusinessUserRole.Finance, false, false, false)]
    [InlineData(BusinessUserRole.Member, false, false, false)]
    public void Policy_enforces_sensitive_KYB_role_boundary(
        BusinessUserRole role,
        bool isOwner,
        bool canRead,
        bool canManage)
    {
        var access = new BusinessAccessContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            role,
            BusinessPermission.All,
            isOwner,
            false,
            1,
            null,
            false);

        Assert.Equal(canRead, BusinessKybAccessPolicy.CanRead(access));
        Assert.Equal(canManage, BusinessKybAccessPolicy.CanManage(access));
    }

    [Fact]
    public void Controller_is_restricted_to_business_identity_roles()
    {
        var authorize = Assert.Single(
            typeof(BusinessKybController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("Business,BusinessAdmin", authorize.Roles);
    }
}
