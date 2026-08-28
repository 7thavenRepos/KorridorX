using System.ComponentModel.DataAnnotations;
using KorridorX.Dtos.Support;
using KorridorX.Services.Support;

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

    [Fact]
    public void Support_evidence_rejects_unsupported_type_and_oversized_file()
    {
        var unsupported = Assert.Throws<InvalidOperationException>(() =>
            SupportEvidenceFilePolicy.ValidateMetadata(
                "evidence.exe",
                "application/octet-stream",
                100,
                10 * 1024 * 1024));
        Assert.Contains("PDF, JPEG, or PNG", unsupported.Message, StringComparison.OrdinalIgnoreCase);

        var oversized = Assert.Throws<InvalidOperationException>(() =>
            SupportEvidenceFilePolicy.ValidateMetadata(
                "evidence.pdf",
                "application/pdf",
                10 * 1024 * 1024 + 1,
                10 * 1024 * 1024));
        Assert.Contains("cannot exceed", oversized.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Support_evidence_requires_content_signature_to_match_declared_type()
    {
        SupportEvidenceFilePolicy.ValidateSignature(
            "application/pdf",
            "%PDF-1.7"u8);

        var mismatch = Assert.Throws<InvalidOperationException>(() =>
            SupportEvidenceFilePolicy.ValidateSignature(
                "application/pdf",
                "not-a-pdf"u8));
        Assert.Contains("does not match", mismatch.Message, StringComparison.OrdinalIgnoreCase);
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
