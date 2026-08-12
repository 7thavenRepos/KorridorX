using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace KorridorX.Services.Auth;

public static class IdentityTokenCodec
{
    public static string Encode(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static bool TryDecode(string encodedToken, out string token)
    {
        token = "";

        if (string.IsNullOrWhiteSpace(encodedToken))
            return false;

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
