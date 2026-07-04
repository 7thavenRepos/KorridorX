namespace KorridorX.Configuration;

public class BlaaizOptions
{
    public string BaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string TokenEndpoint { get; set; } = "/oauth/token";
}