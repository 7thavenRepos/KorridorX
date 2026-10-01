using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class ConsumerExternalAuthOptions
{
    public const string SectionName = "ConsumerExternalAuth";
    public ExternalIdentityProviderOptions Google { get; set; } = new();
    public ExternalIdentityProviderOptions Apple { get; set; } = new();
}

public sealed class ExternalIdentityProviderOptions
{
    public bool Enabled { get; set; }
    public string[] ClientIds { get; set; } = [];
    public string[] PresenterClientIds { get; set; } = [];
}

public sealed class ConsumerExternalAuthOptionsValidator : IValidateOptions<ConsumerExternalAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, ConsumerExternalAuthOptions options)
    {
        var errors = new List<string>();
        foreach (var (provider, value) in new[] { ("google", options.Google), ("apple", options.Apple) })
        {
            if (value is null) { errors.Add($"{provider} options are required."); continue; }
            if (!value.Enabled) { continue; }
            if (value.ClientIds is null || value.ClientIds.Length == 0)
                errors.Add($"Enabled {provider} sign-in requires an explicit ClientIds allowlist.");
            foreach (var id in (value.ClientIds ?? []).Concat(value.PresenterClientIds ?? []))
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || id.Length > 250 ||
                    id.Any(char.IsWhiteSpace) || id.Contains('*') || id.Contains('/') ||
                    (provider == "google" && !id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal)))
                    errors.Add($"{provider} client IDs must be exact, configured public OAuth identifiers.");
            }
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
