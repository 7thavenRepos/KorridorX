using System.Text.Encodings.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace KorridorX.Services.Auth;

public static class AccountSecurityEmailFactory
{
    public static (string Subject, string Body) CreateEmailConfirmation(
        string frontendBaseUrl,
        Guid userId,
        string encodedToken,
        string firstName)
    {
        var link = BuildFragmentLink(
            $"{frontendBaseUrl.TrimEnd('/')}/auth/confirm-email",
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = encodedToken
            });

        return (
            "Confirm your KorridorX email address",
            CreateBody(
                firstName,
                "Confirm your email address",
                "Confirm email",
                link,
                "If you did not create this account, you can ignore this message."));
    }

    public static (string Subject, string Body) CreateMobileEmailConfirmation(
        string mobileDeepLinkBaseUrl,
        Guid userId,
        string encodedToken,
        string firstName)
    {
        var link = BuildFragmentLink(
            $"{mobileDeepLinkBaseUrl.TrimEnd('/')}/confirm-email",
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = encodedToken
            });

        return (
            "Confirm your KorridorX email address",
            CreateBody(
                firstName,
                "Confirm your email address in the KorridorX app",
                "Open KorridorX",
                link,
                "If you did not create this account, you can ignore this message."));
    }

    public static (string Subject, string Body) CreatePasswordReset(
        string frontendBaseUrl,
        Guid userId,
        string encodedToken,
        string firstName)
    {
        var link = BuildFragmentLink(
            $"{frontendBaseUrl.TrimEnd('/')}/auth/reset-password",
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = encodedToken
            });

        return (
            "Reset your KorridorX password",
            CreateBody(
                firstName,
                "Reset your password",
                "Reset password",
                link,
                "If you did not request a password reset, secure your email account and contact KorridorX support."));
    }

    public static (string Subject, string Body) CreateMobilePasswordReset(
        string mobileDeepLinkBaseUrl,
        Guid userId,
        string encodedToken,
        string firstName)
    {
        var link = BuildFragmentLink(
            $"{mobileDeepLinkBaseUrl.TrimEnd('/')}/reset-password",
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = encodedToken
            });

        return (
            "Reset your KorridorX password",
            CreateBody(
                firstName,
                "Reset your password in the KorridorX app",
                "Open KorridorX",
                link,
                "If you did not request a password reset, secure your email account and contact KorridorX support."));
    }

    public static (string Subject, string Body) CreatePasswordChanged(string firstName) =>
        (
            "Your KorridorX password was changed",
            $"<p>Hello {HtmlEncoder.Default.Encode(firstName)},</p>" +
            "<p>Your KorridorX password was changed and all existing sessions were signed out.</p>" +
            "<p>If you did not make this change, contact KorridorX support immediately.</p>");

    public static (string Subject, string Body) CreateMfaEnabled(string firstName) =>
        CreateSecurityNotice(
            "Multi-factor authentication was enabled",
            firstName,
            "Authenticator-based multi-factor authentication was enabled for your KorridorX account.");

    public static (string Subject, string Body) CreateMfaRecoveryCodesRegenerated(
        string firstName) =>
        CreateSecurityNotice(
            "Your KorridorX recovery codes were regenerated",
            firstName,
            "A new set of MFA recovery codes was generated and every previous recovery code is now invalid.");

    public static (string Subject, string Body) CreateMfaReset(string firstName) =>
        CreateSecurityNotice(
            "Multi-factor authentication was reset",
            firstName,
            "Multi-factor authentication was reset and every active KorridorX session was signed out. Privileged accounts must enroll again at the next sign-in.");

    private static string CreateBody(
        string firstName,
        string heading,
        string actionLabel,
        string link,
        string fallback)
    {
        var encoder = HtmlEncoder.Default;
        return $"<p>Hello {encoder.Encode(firstName)},</p>" +
               $"<p>{encoder.Encode(heading)} by selecting the secure link below.</p>" +
               $"<p><a href=\"{encoder.Encode(link)}\">{encoder.Encode(actionLabel)}</a></p>" +
               "<p>This link is single-use and expires automatically.</p>" +
               $"<p>{encoder.Encode(fallback)}</p>";
    }

    private static (string Subject, string Body) CreateSecurityNotice(
        string subject,
        string firstName,
        string message)
    {
        var encoder = HtmlEncoder.Default;
        return (
            subject,
            $"<p>Hello {encoder.Encode(firstName)},</p>" +
            $"<p>{encoder.Encode(message)}</p>" +
            "<p>If you did not make this change, contact KorridorX support immediately.</p>");
    }

    private static string BuildFragmentLink(
        string path,
        IDictionary<string, string?> values)
    {
        var queryLink = QueryHelpers.AddQueryString(path, values);
        var separatorIndex = queryLink.IndexOf('?');
        return separatorIndex < 0
            ? queryLink
            : $"{queryLink[..separatorIndex]}#{queryLink[(separatorIndex + 1)..]}";
    }
}
