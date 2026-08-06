using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public class BlaaizOptionsValidator : IValidateOptions<BlaaizOptions>
{
    public ValidateOptionsResult Validate(string? name, BlaaizOptions options)
    {
        if (!options.IsEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("Blaaiz:BaseUrl must be a valid HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            errors.Add("Blaaiz:ClientId is required when Blaaiz is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            errors.Add("Blaaiz:ClientSecret is required when Blaaiz is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.TokenEndpoint))
        {
            errors.Add("Blaaiz:TokenEndpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Scopes))
        {
            errors.Add("Blaaiz:Scopes is required when Blaaiz is enabled.");
        }

        if (options.TimeoutSeconds is < 5 or > 120)
        {
            errors.Add("Blaaiz:TimeoutSeconds must be between 5 and 120.");
        }

        if (options.TokenRefreshBufferSeconds is < 0 or > 300)
        {
            errors.Add("Blaaiz:TokenRefreshBufferSeconds must be between 0 and 300.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
