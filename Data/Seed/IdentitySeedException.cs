using Microsoft.AspNetCore.Identity;

namespace KorridorX.Data.Seed;

public sealed class IdentitySeedException : InvalidOperationException
{
    public IdentitySeedException(string message) : base(message)
    {
    }

    public static IdentitySeedException FromIdentityResult(
        string operation,
        IdentityResult result)
    {
        var details = string.Join(
            "; ",
            result.Errors.Select(x => string.IsNullOrWhiteSpace(x.Code) ? x.Description : x.Code));

        return new IdentitySeedException(
            string.IsNullOrWhiteSpace(details)
                ? $"{operation} failed."
                : $"{operation} failed: {details}.");
    }
}
