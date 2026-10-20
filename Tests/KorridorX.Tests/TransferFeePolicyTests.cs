using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Services.Fx;
using Microsoft.AspNetCore.Authorization;

namespace KorridorX.Tests;

public sealed class TransferFeePolicyTests
{
    private static SaveTransferFeeRequestDto Request() => new()
    { TransferType = TransferType.ConsumerToConsumer, MinAmount = 0, FixedFee = 0, PercentageFee = 0 };

    [Fact]
    public void Explicit_zero_fee_is_valid_and_does_not_require_a_maximum() => TransferFeePolicy.Validate(Request(), 2);

    [Theory]
    [InlineData(-1, 10, 0, 0)] [InlineData(10, 5, 0, 0)] [InlineData(0, 0, 0, 0)]
    [InlineData(0, 10, -1, 0)] [InlineData(0, 10, 0, -1)] [InlineData(0, 10, 0, 101)]
    public void Negative_fees_or_invalid_ranges_are_rejected(decimal min, decimal max, decimal fixedFee, decimal percentage)
    {
        var request = Request(); request.MinAmount = min; request.MaxAmount = max;
        request.FixedFee = fixedFee; request.PercentageFee = percentage;
        Assert.Throws<InvalidOperationException>(() => TransferFeePolicy.Validate(request, 2));
    }

    [Fact]
    public void Unsupported_type_or_precision_is_rejected()
    {
        var request = Request(); request.TransferType = (TransferType)99;
        Assert.Throws<InvalidOperationException>(() => TransferFeePolicy.Validate(request, 2));
        request = Request(); request.FixedFee = 0.001m;
        Assert.Throws<InvalidOperationException>(() => TransferFeePolicy.Validate(request, 2));
        request = Request(); request.PercentageFee = 0.0000001m;
        Assert.Throws<InvalidOperationException>(() => TransferFeePolicy.Validate(request, 2));
    }

    [Fact]
    public void Active_ranges_include_both_endpoints_and_an_unbounded_range_overlaps_later_ranges()
    {
        Assert.True(TransferFeePolicy.Overlaps(0, 100, 100, 200));
        Assert.True(TransferFeePolicy.Overlaps(0, null, 200, 500));
        Assert.True(TransferFeePolicy.Overlaps(100, 200, 0, null));
        Assert.False(TransferFeePolicy.Overlaps(0, 100, 100.01m, null));
    }

    [Fact]
    public void Revisions_round_trip_at_postgresql_precision_and_advance_on_every_change()
    {
        var fee = new TransferFee { CreatedAt = DateTime.UtcNow };
        fee.LastUpdatedAt = TransferFeePolicy.NextUpdate(fee);
        Assert.Equal(0, fee.LastUpdatedAt.Value.Ticks % 10);
        var revision = TransferFeePolicy.Revision(fee);
        TransferFeePolicy.RequireRevision(fee, revision);
        fee.LastUpdatedAt = TransferFeePolicy.NextUpdate(fee);
        Assert.NotEqual(revision, TransferFeePolicy.Revision(fee));
        Assert.Throws<InvalidOperationException>(() => TransferFeePolicy.RequireRevision(fee, revision));
    }

    [Fact]
    public void Omitted_fee_fields_cannot_silently_become_a_zero_fee()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SaveTransferFeeRequestDto>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SaveTransferFeeRequestDto>("{\"MinAmount\":0,\"FixedFee\":0}"));
    }

    [Fact]
    public void Fee_mutations_are_restricted_to_administrators()
    {
        var controller = typeof(AdminTransferFeesController);
        Assert.Contains("Operations", controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles!);
        foreach (var action in new[] { "Create", "Update", "Activate", "Deactivate" })
            Assert.Equal("Admin,SuperAdmin", controller.GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }
}
