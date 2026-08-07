using KorridorX.Configuration;
using KorridorX.Models.Enums;

namespace KorridorX.Tests;

public class SupportOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var validator = new SupportOptionsValidator();
        var result = validator.Validate(null, new SupportOptions());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MissingUrgentTarget_Fails()
    {
        var options = new SupportOptions();
        options.SlaTargets.Remove(SupportTicketPriority.Urgent);
        var result = new SupportOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
    }
}
