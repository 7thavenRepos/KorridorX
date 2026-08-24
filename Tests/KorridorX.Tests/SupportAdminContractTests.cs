using System.ComponentModel.DataAnnotations;
using KorridorX.Dtos.Support;

namespace KorridorX.Tests;

public sealed class SupportAdminContractTests
{
    [Fact]
    public void Ticket_assignment_requires_reason()
    {
        var request = new AssignSupportTicketRequestDto
        {
            AssignedToUserId = Guid.NewGuid(),
            Reason = ""
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void Ticket_update_requires_reason()
    {
        var request = new UpdateSupportTicketRequestDto
        {
            Reason = ""
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void Investigation_update_requires_reason()
    {
        var request = new UpdateTransferInvestigationRequestDto
        {
            Reason = ""
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void Manual_sla_run_requires_valid_batch_and_reason()
    {
        var request = new ProcessSupportSlaRequestDto
        {
            BatchSize = 0,
            Reason = ""
        };

        Assert.False(IsValid(request));

        request.BatchSize = 100;
        request.Reason = "Manual operations review.";

        Assert.True(IsValid(request));
    }

    private static bool IsValid(object value)
    {
        var context = new ValidationContext(value);
        var results = new List<ValidationResult>();

        return Validator.TryValidateObject(
            value,
            context,
            results,
            validateAllProperties: true);
    }
}