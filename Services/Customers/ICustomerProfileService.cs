using KorridorX.Dtos.Customers;

namespace KorridorX.Services.Customers;

public interface ICustomerProfileService
{
    Task<CustomerProfileDto> GetMyProfileAsync(Guid userId, CancellationToken ct = default);
    Task<CustomerProfileDto> UpdateMyProfileAsync(Guid userId, UpdateCustomerProfileRequestDto request, CancellationToken ct = default);
}