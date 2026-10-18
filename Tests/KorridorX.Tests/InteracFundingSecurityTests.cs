using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Notifications;
using KorridorX.Models.Payments;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests;

public sealed class InteracFundingSecurityTests
{
    [Theory]
    [InlineData("  Kay+business@EXAMPLE.COM  ", "kay+business@example.com")]
    [InlineData("first.last@example.com", "first.last@example.com")]
    [InlineData("firstname@example.com", "firstname@example.com")]
    public void Email_canonicalization_preserves_bank_specific_alias_identity(string input, string expected) =>
        Assert.Equal(expected, InteracSecurity.NormalizeEmail(input));

    [Theory]
    [InlineData("Kay <kay@example.com>")]
    [InlineData("a@example.com,b@example.com")]
    [InlineData("a@example.com\r\nBcc: b@example.com")]
    [InlineData("not-an-email")]
    public void Invalid_or_ambiguous_address_input_is_rejected(string input) =>
        Assert.Throws<InvalidOperationException>(() => InteracSecurity.NormalizeEmail(input));

    [Fact]
    public void Code_is_six_digits_salted_hashed_expires_and_cannot_be_consumed_twice()
    {
        var row = new InteracSendingEmail(); var now = DateTime.UtcNow;
        var code = InteracSecurity.IssueCode(row, now);
        Assert.Matches("^[0-9]{6}$", code); Assert.NotEqual(code, row.CodeHash);
        Assert.True(InteracSecurity.CodeMatches(row, code, now.AddMinutes(9)));
        Assert.False(InteracSecurity.CodeMatches(row, code, now.AddMinutes(10)));
        row.Status = "Verified"; InteracSecurity.ClearCode(row);
        Assert.False(InteracSecurity.CodeMatches(row, code, now.AddMinutes(1)));
        Assert.Null(row.CodeHash); Assert.Null(row.CodeSalt); Assert.Null(row.CodeExpiresAt);
    }

    [Fact]
    public void Code_attempt_limit_is_enforced_even_with_the_correct_code()
    {
        var sender = new InteracSendingEmail(); var now = DateTime.UtcNow;
        var code = InteracSecurity.IssueCode(sender, now); sender.FailedAttempts = 5;
        Assert.False(InteracSecurity.CodeMatches(sender, code, now));
        Assert.False(InteracSecurity.CodeMatches(sender, "12345x", now));
        Assert.Equal(now.AddSeconds(60), sender.CanResendAt);
    }

    [Fact]
    public void Verification_email_body_is_redacted_in_existing_notification_APIs()
    {
        var notice = new NotificationMessage { Body = "secret-code-content", RelatedEntityType = NotificationSecurityPolicy.AccountSecurityEntityType };
        Assert.Equal(NotificationSecurityPolicy.RedactedBody, NotificationSecurityPolicy.BodyForApi(notice));
    }

    [Theory]
    [InlineData(100d, 97.5d, 2.5d, true)]
    [InlineData(100d, 100d, 0d, true)]
    [InlineData(100d, null, 0d, false)]
    [InlineData(100d, 99d, null, false)]
    [InlineData(100d, 100d, 1d, false)]
    [InlineData(100d, 0d, 100d, false)]
    [InlineData(100d, 101d, -1d, false)]
    [InlineData(100.001d, 100d, 0.001d, false)]
    public void Unsolicited_credit_requires_explicit_consistent_CAD_net_gross_and_fee(double gross, double? net, double? fee, bool valid)
    {
        var value = new BlaaizTransactionData { Amount = (decimal)gross, AmountWithoutFee = net.HasValue ? (decimal)net.Value : null,
            Fee = fee.HasValue ? (decimal)fee.Value : null };
        Assert.Equal(valid, InteracDepositService.ValidateUnsolicitedMoney(value) is null);
    }

    [Fact]
    public void Deposit_parser_uses_actual_sender_and_receiver_and_never_event_ID_or_generic_email()
    {
        var parsed = InteracDepositService.ReadEvidence("""
            {"event_id":"event-only","transaction_id":"deposit-123","transaction_currency":"CAD","transaction_amount":"100.00",
             "transaction_amount_without_fee":"97.50","transaction_fee":"2.50","email":"untrusted@example.test",
             "payee_collection_email":"RECEIVE@example.test","source_information":{"collection_email":"Sender@example.test"}}
            """);
        Assert.Equal("deposit-123", parsed.TransactionId); Assert.Equal("sender@example.test", parsed.SenderEmail);
        Assert.Equal("receive@example.test", parsed.ReceivingEmail); Assert.Equal(97.50m, parsed.Net);
        Assert.Null(InteracDepositService.ReadEvidence("{\"event_id\":\"not-a-transaction\",\"currency\":\"CAD\",\"email\":\"sender@example.test\"}").SenderEmail);
    }

    [Fact]
    public void Contradictory_signed_and_confirmed_sender_or_receiving_details_fail_closed()
    {
        var value = Transaction();
        var evidence = new InteracWebhookEvidence(value.Id, value.Reference, "SUCCESSFUL", "CAD", 100m, 97.5m, 2.5m, "other@example.test", value.PayeeCollectionEmail);
        Assert.NotNull(InteracDepositService.ValidateConfirmation(evidence, value));
        evidence = evidence with { SenderEmail = value.SourceInformation!.CollectionEmail, ReceivingEmail = "wrong-route@example.test" };
        Assert.NotNull(InteracDepositService.ValidateConfirmation(evidence, value));
    }

    [Fact]
    public void Provider_activation_requires_exact_registration_active_status_and_CAD_wallet_business()
    {
        var route = new InteracReceivingRoute { ProviderRegistrationId = Guid.NewGuid().ToString(), NormalizedReceivingEmail = "receive@example.test", ProviderWalletId = "cad-wallet" };
        var registration = new BlaaizInteracAutoDepositEmailData { Id = route.ProviderRegistrationId, Email = route.NormalizedReceivingEmail, Status = "ACTIVE" };
        var wallet = new BlaaizWalletData { Id = "cad-wallet", IsActive = true, Currency = "CAD", BusinessId = "our-business" };
        Assert.Null(InteracAdminService.ValidateProviderRoute(route, registration, wallet));
        registration.Status = "PENDING"; Assert.NotNull(InteracAdminService.ValidateProviderRoute(route, registration, wallet));
        registration.Status = "ACTIVE"; registration.Email = "other@example.test"; Assert.NotNull(InteracAdminService.ValidateProviderRoute(route, registration, wallet));
        Assert.NotNull(InteracAdminService.ValidateProviderRoute(route, null, wallet));
        Assert.NotNull(InteracAdminService.ValidateProviderRoute(route, registration, null));
    }

    [Fact]
    public void Existing_request_keeps_gross_contract_while_unsolicited_collection_requires_confirmed_net()
    {
        var confirmed = Transaction();
        var request = new Collection { CurrencyCode = "CAD", Amount = 100m, ProviderReference = confirmed.Reference };
        ProviderCollectionCreditGuard.ValidateExistingCollection(request, confirmed.Id, confirmed);
        request.RelatedEntityType = nameof(InteracDeposit);
        Assert.Throws<InvalidOperationException>(() => ProviderCollectionCreditGuard.ValidateExistingCollection(request, confirmed.Id, confirmed));
        request.Amount = 97.5m; ProviderCollectionCreditGuard.ValidateExistingCollection(request, confirmed.Id, confirmed);
    }

    [Fact]
    public void Missing_provider_transaction_identity_is_never_an_unguarded_Blaaiz_credit()
    {
        var collection = new Collection { ProviderCode = "BLAAIZ", CurrencyCode = "CAD", PaymentMethod = PaymentMethod.Interac, Amount = 10m };
        Assert.True(ProviderCollectionCreditGuard.Applies(collection, null));
        Assert.Throws<InvalidOperationException>(() => ProviderCollectionCreditGuard.ApplyLedgerIdentity(collection, new LedgerTransaction()));
    }

    [Fact]
    public void Ledger_deduplication_key_is_shared_by_requested_and_unsolicited_paths()
    {
        var requested = new Collection { ProviderCode = "BLAAIZ", ProviderCollectionId = "immutable-provider-deposit", Amount = 100m };
        var unsolicited = new Collection { ProviderCode = "Blaaiz", ProviderCollectionId = "immutable-provider-deposit", Amount = 97.5m, RelatedEntityType = nameof(InteracDeposit) };
        var a = new LedgerTransaction(); var b = new LedgerTransaction();
        ProviderCollectionCreditGuard.ApplyLedgerIdentity(requested, a); ProviderCollectionCreditGuard.ApplyLedgerIdentity(unsolicited, b);
        Assert.Equal(a.IdempotencyScope, b.IdempotencyScope); Assert.Equal(a.IdempotencyKey, b.IdempotencyKey);
        Assert.NotEqual(a.IdempotencyRequestHash, b.IdempotencyRequestHash);
    }

    [Fact]
    public void Ownership_and_provider_claim_have_database_unique_constraints()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused").Options);
        var ownership = db.Model.FindEntityType(typeof(InteracSenderOwnership))!;
        Assert.Contains(ownership.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["RouteId", "NormalizedEmail"]));
        var claim = db.Model.FindEntityType(typeof(ProviderCollectionCreditClaim))!;
        Assert.Contains(claim.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["ProviderCode", "Environment", "ConnectionKey", "ProviderTransactionId"]));
    }

    [Fact]
    public void Admin_review_is_privileged_and_exposes_no_credit_refund_or_reassignment_action()
    {
        Assert.Equal("SuperAdmin", typeof(AdminInteracController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.DoesNotContain(typeof(AdminInteracController).GetMethods(), x => x.DeclaringType == typeof(AdminInteracController) &&
            (x.Name.Contains("Credit", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Refund", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Reassign", StringComparison.OrdinalIgnoreCase)));
    }

    private static BlaaizTransactionData Transaction() => new() { Id = "deposit-123", Reference = "reference-123", Type = "collection",
        Status = "SUCCESSFUL", Currency = "CAD", Amount = 100m, AmountWithoutFee = 97.5m, Fee = 2.5m,
        PayeeCollectionEmail = "receive@example.test", SourceInformation = new() { CollectionEmail = "sender@example.test" } };
}
