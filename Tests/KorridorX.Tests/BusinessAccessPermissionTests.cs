using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;

namespace KorridorX.Tests;

public class BusinessAccessPermissionTests
{
    [Fact]
    public void Owner_HasAllPermissions()
    {
        var permissions = BusinessAccessService.DefaultPermissions(BusinessUserRole.Owner);

        Assert.Equal(BusinessPermission.All, permissions);
    }

    [Fact]
    public void Finance_CanManageFundingButCannotApproveTransfers()
    {
        var permissions = BusinessAccessService.DefaultPermissions(BusinessUserRole.Finance);

        Assert.True(permissions.HasFlag(BusinessPermission.ManageFunding));
        Assert.False(permissions.HasFlag(BusinessPermission.ApproveTransfers));
    }

    [Fact]
    public void Compliance_CanApproveButCannotCreateTransfers()
    {
        var permissions = BusinessAccessService.DefaultPermissions(BusinessUserRole.Compliance);

        Assert.True(permissions.HasFlag(BusinessPermission.ApproveTransfers));
        Assert.False(permissions.HasFlag(BusinessPermission.CreateTransfers));
    }
}
