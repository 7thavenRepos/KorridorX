using KorridorX.Services.WalletWithdrawals;
using Xunit;

namespace KorridorX.Tests;

public sealed class WalletWithdrawalRecoveryPolicyTests
{
    private const string NoteRejection =
        "{\"message\":\"The note must not be greater than 50 characters.\",\"errors\":{\"note\":[\"The note must not be greater than 50 characters.\"]}}";

    [Fact]
    public void Exact_note_length_validation_is_identified_without_exposing_raw_payload()
    {
        Assert.Equal(WalletWithdrawalRecoveryPolicy.BlaaizNoteLimitMessage,
            WalletWithdrawalRecoveryPolicy.ReadKnownNoteValidationError(NoteRejection));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{malformed")]
    [InlineData("{\"errors\":{\"bank_id\":[\"Invalid recipient account\"]}}")]
    [InlineData("{\"message\":\"The note must not be greater than 50 characters.\"}")]
    public void Unexpected_provider_errors_are_not_trusted_or_exposed(string? json)
    {
        Assert.Null(WalletWithdrawalRecoveryPolicy.ReadKnownNoteValidationError(json));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(3, 240)]
    [InlineData(4, 480)]
    [InlineData(5, 960)]
    [InlineData(6, 1920)]
    [InlineData(7, 3600)]
    [InlineData(8, 3600)]
    [InlineData(1000, 3600)]
    public void Consecutive_provider_not_found_responses_have_capped_cooldown(int count, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds),
            WalletWithdrawalRecoveryPolicy.CooldownAfterNotFound(count));
    }

    [Fact]
    public void Automatic_lookup_stays_deferred_until_cooldown_expires()
    {
        var last = new DateTime(2026, 10, 25, 1, 36, 33, DateTimeKind.Utc);
        Assert.True(WalletWithdrawalRecoveryPolicy.ShouldDeferAutomaticLookup(
            last.AddSeconds(59), last, 1));
        Assert.False(WalletWithdrawalRecoveryPolicy.ShouldDeferAutomaticLookup(
            last.AddSeconds(60), last, 1));
        Assert.True(WalletWithdrawalRecoveryPolicy.ShouldDeferAutomaticLookup(
            last.AddMinutes(59), last, 8));
        Assert.False(WalletWithdrawalRecoveryPolicy.ShouldDeferAutomaticLookup(
            last.AddHours(1), last, 8));
    }
}
