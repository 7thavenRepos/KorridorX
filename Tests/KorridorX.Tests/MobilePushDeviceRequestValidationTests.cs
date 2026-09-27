using System.ComponentModel.DataAnnotations;
using KorridorX.Dtos.Notifications;
using Xunit;

namespace KorridorX.Tests;

public class MobilePushDeviceRequestValidationTests
{
    [Fact]
    public void
        Valid_registration_request_passes_data_annotations()
    {
        var request =
            new RegisterMobilePushDeviceRequestDto
            {
                Platform = "Android",
                PushToken = "token-123",
                DeviceFingerprint =
                    "device-fingerprint",
                DeviceName =
                    "KorridorX Android"
            };

        Assert.Empty(
            Validate(request));
    }

    [Fact]
    public void
        Registration_requires_all_device_identity_values()
    {
        var request =
            new RegisterMobilePushDeviceRequestDto
            {
                Platform = "",
                PushToken = "",
                DeviceFingerprint = "",
                DeviceName = ""
            };

        Assert.NotEmpty(
            Validate(request));
    }

    [Fact]
    public void
        Registration_rejects_tokens_beyond_storage_contract()
    {
        var request =
            new RegisterMobilePushDeviceRequestDto
            {
                Platform = "Android",
                PushToken =
                    new string('x', 4097),
                DeviceFingerprint =
                    "device-fingerprint",
                DeviceName =
                    "KorridorX Android"
            };

        Assert.Contains(
            Validate(request),
            result =>
                result.MemberNames.Contains(
                    nameof(
                        RegisterMobilePushDeviceRequestDto
                            .PushToken)));
    }

    private static
        IReadOnlyList<ValidationResult>
        Validate(
            object instance)
    {
        var results =
            new List<ValidationResult>();

        Validator.TryValidateObject(
            instance,
            new ValidationContext(instance),
            results,
            validateAllProperties: true);

        return results;
    }
}
