namespace KorridorX.Dtos.Customers;

public record UpdateCustomerProfileRequestDto
(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateTime? DateOfBirth,
    string? PhoneNumber,
    string CountryCode,
    string? StateOrProvince,
    string? City,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode
);