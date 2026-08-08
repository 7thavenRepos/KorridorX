using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KorridorX.Infrastructure;

namespace KorridorX.Tests.Infrastructure;

public static class ApiTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static void UseBearerToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static async Task<ApiResponse<T>> ReadApiResponseAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("The API returned an empty response body.");
    }

    public static async Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client,
        string requestUri,
        T payload,
        CancellationToken ct = default) =>
        await client.PostAsJsonAsync(requestUri, payload, JsonOptions, ct);

    public static async Task<HttpResponseMessage> PutJsonAsync<T>(
        this HttpClient client,
        string requestUri,
        T payload,
        CancellationToken ct = default) =>
        await client.PutAsJsonAsync(requestUri, payload, JsonOptions, ct);
}
