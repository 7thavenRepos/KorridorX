using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessContext;

public record AvailableBusinessContextDto(
    Guid BusinessProfileId,
    string BusinessName,
    BusinessUserRole Role,
    BusinessPermission Permissions,
    bool IsOwner,
    KybStatus KybStatus,
    bool IsSelected);
