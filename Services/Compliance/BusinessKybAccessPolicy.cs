using KorridorX.Models.Customers;
using KorridorX.Services.BusinessTransfers;

namespace KorridorX.Services.Compliance;

public static class BusinessKybAccessPolicy
{
    public static bool CanRead(BusinessAccessContext access) =>
        access.IsOwner ||
        access.Role is BusinessUserRole.Admin or BusinessUserRole.Compliance;

    public static bool CanManage(BusinessAccessContext access) =>
        access.IsOwner || access.Role == BusinessUserRole.Admin;
}
