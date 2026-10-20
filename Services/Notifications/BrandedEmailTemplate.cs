using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace KorridorX.Services.Notifications;

public sealed record BrandedEmailContent(string Html, string PlainText);

/// <summary>
/// The common presentation boundary for every SMTP email. Stored notification
/// bodies stay as compact content fragments for in-app history and push policy.
/// </summary>
public static class BrandedEmailTemplate
{
    public static BrandedEmailContent Render(string subject, string body, string frontendBaseUrl)
    {
        var encode = HtmlEncoder.Default;
        var appUrl = ResolveAppUrl(frontendBaseUrl);
        var title = encode.Encode(subject);
        var content = Regex.IsMatch(body, @"</?(?:p|a|div|table|ul|ol|li|br|strong|em|h[1-6])(?:\s|/?>)", RegexOptions.IgnoreCase)
            ? body
            : "<p>" + string.Join("<br />", body.Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n').Select(encode.Encode)) + "</p>";
        var brand = appUrl is null
            ? "<span style=\"font-size:26px;font-weight:700;color:#ffebef;\">KorridorX</span>"
            : $"<img src=\"{encode.Encode(appUrl + "/assets/brand/korridorx-symbol.png")}\" width=\"25\" height=\"31\" alt=\"\" style=\"display:inline-block;vertical-align:middle;border:0;\" />" +
              $" <img src=\"{encode.Encode(appUrl + "/assets/brand/korridorx-logo-light.png")}\" width=\"140\" height=\"31\" alt=\"KorridorX\" style=\"display:inline-block;vertical-align:middle;border:0;\" />";
        var action = appUrl is null ? "" : $"""
            <p style="margin:28px 0 0;"><a href="{encode.Encode(appUrl)}" style="display:inline-block;background:#ffebef;color:#005b31;padding:15px 25px;border-radius:32px;font-size:16px;font-weight:700;text-decoration:none;">Open KorridorX</a></p>
            """;
        var support = appUrl is null ? "Open KorridorX and choose Support if you need help."
            : $"Need help? Open <a href=\"{encode.Encode(appUrl)}\" style=\"color:#005b31;text-decoration:underline;\">KorridorX</a> and choose Support.";
        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width,initial-scale=1" />
              <meta name="color-scheme" content="light" />
              <title>{{title}}</title>
              <style>
                .kx-email-content p { margin:0 0 18px; }
                .kx-email-content a { color:#ffebef; overflow-wrap:anywhere; }
                .kx-email-content table { width:100%; }
                @media only screen and (max-width:620px) {
                  .kx-email-padding { padding:28px 24px !important; }
                  .kx-email-title { font-size:30px !important; }
                }
              </style>
            </head>
            <body style="margin:0;padding:0;background:#ffffff;color:#17241d;font-family:Arial,Helvetica,sans-serif;">
              <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;">{{title}}</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#ffffff;">
                <tr><td align="center" style="padding:24px 12px;">
                  <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;">
                    <tr><td class="kx-email-padding" style="padding:38px 40px 42px;background:#005b31;border-radius:24px;color:#ffebef;">
                      <p style="margin:0 0 38px;">{{brand}}</p>
                      <h1 class="kx-email-title" style="font-size:36px;line-height:1.15;font-weight:700;margin:0 0 28px;color:#ffebef;">{{title}}</h1>
                      <div class="kx-email-content" style="font-size:17px;line-height:1.65;color:#ffebef;overflow-wrap:anywhere;">{{content}}</div>
                      {{action}}
                    </td></tr>
                    <tr><td class="kx-email-padding" style="padding:30px 40px 12px;font-size:13px;line-height:1.65;color:#40473f;">
                      <p style="margin:0 0 18px;">{{support}}</p>
                      <p style="margin:0 0 18px;border-top:1px solid #e5e9e6;padding-top:18px;">This is an account or transaction notification from KorridorX. Keep your password, PIN and verification codes private.</p>
                      <p style="margin:0;font-weight:700;color:#005b31;">KorridorX</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
        var plain = subject + "\n\n" + ToPlainText(content) +
            (appUrl is null ? "" : "\n\nOpen KorridorX: " + appUrl) +
            "\n\nNeed help? Open KorridorX and choose Support.\nKeep your password, PIN and verification codes private.";
        return new(html, plain);
    }

    private static string ToPlainText(string html)
    {
        // Keep the href alongside the text so actions still work in plain-text
        // clients. Action URLs and one-time codes are never rewritten.
        var linked = Regex.Replace(html, "<a\\b[^>]*href=[\"'](?<url>[^\"']+)[\"'][^>]*>(?<label>.*?)</a>",
            match => match.Groups["label"].Value + " (" + match.Groups["url"].Value + ")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var lines = Regex.Replace(linked, @"<br\s*/?>|</(?:p|div|li|h[1-6])\s*>", "\n", RegexOptions.IgnoreCase);
        return WebUtility.HtmlDecode(Regex.Replace(lines, "<[^>]+>", "")).Trim();
    }

    private static string? ResolveAppUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            ? value.TrimEnd('/') : null;
}
