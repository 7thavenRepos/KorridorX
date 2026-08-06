using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Customers;

public record CustomerProfileDto
(
    Guid Id,
    Guid UserId,
    CustomerType CustomerType,
    string FirstName,
    string LastName,
    string? MiddleName,
    DateTime? DateOfBirth,
    string? PhoneNumber,
    string? Email,
    string CountryCode,
    string? StateOrProvince,
    string? City,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    KycStatus KycStatus,
    DateTime CreatedAt
);