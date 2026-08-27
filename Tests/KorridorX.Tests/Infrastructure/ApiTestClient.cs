using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KorridorX.Dtos.Auth;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

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

    public static async Task EnsureSuccessWithBodyAsync(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Response status code does not indicate success: " +
            $"{(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}",
            null,
            response.StatusCode);
    }

    public static async Task<RegistrationResultDto> RegisterAndConfirmAsync(
        this HttpClient client,
        IServiceProvider services,
        RegisterRequestDto request,
        CancellationToken ct = default)
    {
        var (registrationPath, registrationPayload) = ToChannelRegistration(request);
        using var registerResponse = await client.PostJsonAsync(
            registrationPath,
            registrationPayload,
            ct);
        await registerResponse.EnsureSuccessWithBodyAsync(ct);

        var registerEnvelope = await registerResponse
            .ReadApiResponseAsync<RegistrationResultDto>(ct);

        var registration = registerEnvelope.Data
            ?? throw new InvalidOperationException("Registration returned no data.");

        if (registration.EmailConfirmationRequired)
        {
            await using var scope = services.CreateAsyncScope();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync(request.Email)
                ?? throw new InvalidOperationException("Registered user was not found.");

            var rawToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

            using var confirmationResponse = await client.PostJsonAsync(
                "/api/auth/email-confirmation/confirm",
                new EmailConfirmationDto(
                    user.Id,
                    IdentityTokenCodec.Encode(rawToken)),
                ct);

            await confirmationResponse.EnsureSuccessWithBodyAsync(ct);
        }

        return registration;
    }

    public static async Task<(RegistrationResultDto Registration, AuthResponseDto Authentication)>
        RegisterConfirmAndLoginAsync(
            this HttpClient client,
            IServiceProvider services,
            RegisterRequestDto request,
            CancellationToken ct = default)
    {
        var (registrationPath, registrationPayload) = ToChannelRegistration(request);
        using var registerResponse = await client.PostJsonAsync(
            registrationPath,
            registrationPayload,
            ct);
        await registerResponse.EnsureSuccessWithBodyAsync(ct);
        var registerEnvelope = await registerResponse.ReadApiResponseAsync<RegistrationResultDto>(ct);
        var registration = registerEnvelope.Data
            ?? throw new InvalidOperationException("Registration returned no data.");

        if (registration.EmailConfirmationRequired)
        {
            await using var scope = services.CreateAsyncScope();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(request.Email)
                ?? throw new InvalidOperationException("Registered user was not found.");
            var rawToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

            using var confirmationResponse = await client.PostJsonAsync(
                "/api/auth/email-confirmation/confirm",
                new EmailConfirmationDto(user.Id, IdentityTokenCodec.Encode(rawToken)),
                ct);
            await confirmationResponse.EnsureSuccessWithBodyAsync(ct);
        }

        using var loginResponse = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequestDto(
                request.Email,
                request.Password,
                "rc-device",
                "RC Test Device"),
            ct);
        await loginResponse.EnsureSuccessWithBodyAsync(ct);
        var loginEnvelope = await loginResponse.ReadApiResponseAsync<LoginResultDto>(ct);
        var login = loginEnvelope.Data
            ?? throw new InvalidOperationException("Login returned no result.");
        if (login.Status != LoginStatuses.Authenticated)
            throw new InvalidOperationException($"Login returned unexpected status '{login.Status}'.");
        var authentication = login.Authentication
            ?? throw new InvalidOperationException("Login returned no authentication data.");

        return (registration, authentication);
    }

    private static (string Path, ChannelRegistrationRequestDto Payload)
        ToChannelRegistration(RegisterRequestDto request)
    {
        var path = request.UserType switch
        {
            UserType.Consumer => "/api/auth/register/consumer",
            UserType.Business => "/api/auth/register/business",
            _ => throw new InvalidOperationException(
                $"The test registration helper does not support {request.UserType} registration.")
        };

        return (
            path,
            new ChannelRegistrationRequestDto(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password,
                request.PhoneNumber,
                request.CountryCode));
    }
}
