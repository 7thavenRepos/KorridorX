using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Exceptions;
using KorridorX.Services.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Fx;

public sealed class BlaaizFxRateProvider : IFxRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly IBlaaizFxRateTokenService _tokens;
    private readonly IProviderRequestAuditService _audit;
    private readonly BlaaizFxConnection _connection;
    private readonly TimeProvider _clock;

    public BlaaizFxRateProvider(HttpClient httpClient, IBlaaizFxRateTokenService tokens,
        IProviderRequestAuditService audit, IOptions<BlaaizOptions> options,
        IHostEnvironment environment, TimeProvider? clock = null)
    {
        _httpClient = httpClient;
        _tokens = tokens;
        _audit = audit;
        _connection = new BlaaizFxConnection(options.Value, environment.EnvironmentName);
        _clock = clock ?? TimeProvider.System;
    }

    public string ProviderCode => "Blaaiz";
    public string ConnectionKey => _connection.Key;
    public bool IsAvailable => _connection.IsAvailable;

    public async Task<FxProviderRate> GetRateAsync(string sourceCurrencyCode, string destinationCurrencyCode,
        string? providerRateId = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var source = BlaaizFxRateParser.NormalizeCurrency(sourceCurrencyCode);
        var destination = BlaaizFxRateParser.NormalizeCurrency(destinationCurrencyCode);
        if (source == destination) throw new InvalidOperationException("FX source and destination currencies must differ.");
        var selectedId = BlaaizFxRateParser.NormalizeOptionalRateId(providerRateId);
        _connection.EnsureAvailable();
        EnsureSameTokenConnection();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_connection.TimeoutSeconds), _clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        var operationCt = deadline.Token;
        var endpoint = "/api/external/rate?search_term=" + Uri.EscapeDataString(source + "/" + destination);
        Guid? requestLogId = null;
        var timer = Stopwatch.StartNew();
        int? status = null;
        string? auditResponse = null;
        try
        {
            var token = await _tokens.GetRatesReadAccessTokenAsync(operationCt);
            EnsureSameTokenConnection();
            if (string.IsNullOrWhiteSpace(token))
                throw new ProviderIntegrationException("Blaaiz FX authentication did not return a usable access token.");
            requestLogId = await _audit.StartAsync(KorridorX.Models.Enums.ProviderCode.Blaaiz, endpoint, "GET",
                JsonSerializer.Serialize(new { authorization = "Bearer ***REDACTED***", accept = "application/json" }),
                JsonSerializer.Serialize(new { sourceCurrencyCode = source, destinationCurrencyCode = destination, providerRateId = selectedId }), ct: operationCt);
            var requestedAt = _clock.GetUtcNow().UtcDateTime;
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_connection.ApiBaseUri!, endpoint));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("KorridorX/1.0");
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operationCt);
            status = (int)response.StatusCode;
            var raw = await BlaaizFxRateTokenService.ReadBoundedAsync(response.Content, 2 * 1024 * 1024, operationCt);
            auditResponse = raw.Replace(token, "***REDACTED***", StringComparison.Ordinal);
            var receivedAt = _clock.GetUtcNow().UtcDateTime;
            if (!response.IsSuccessStatusCode)
                throw new ProviderIntegrationException(response.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "Blaaiz denied the FX rate read. Confirm rates:read and API/IP access for this provider connection."
                    : "Blaaiz rejected the live FX rate read.");
            var selected = BlaaizFxRateParser.Parse(raw, source, destination, selectedId);
            EnsureSameTokenConnection();
            operationCt.ThrowIfCancellationRequested();
            await _audit.CompleteAsync(requestLogId.Value, status.Value, auditResponse, timer.ElapsedMilliseconds, operationCt);
            return new FxProviderRate(source, destination, selected.Rate, selected.ProviderRateId,
                selected.ProviderTimestampUtc, requestedAt, receivedAt, ConnectionKey, requestLogId.Value,
                selected.SendEnabled, selected.SwapEnabled)
            { ProviderCategory = selected.Category, ProviderRateType = selected.RateType };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FailAuditAsync(requestLogId, status, auditResponse, "The live FX rate read was canceled.", timer.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception) when (exception is ProviderIntegrationException or HttpRequestException or
                                         OperationCanceledException or IOException)
        {
            var message = exception is ProviderIntegrationException ? exception.Message :
                exception is OperationCanceledException ? "The live Blaaiz FX rate read timed out." : "Unable to complete the live Blaaiz FX rate read.";
            await FailAuditAsync(requestLogId, status, auditResponse, message, timer.ElapsedMilliseconds);
            throw new ProviderIntegrationException(message, status, requestLogId: requestLogId);
        }
    }

    private async Task FailAuditAsync(Guid? requestLogId, int? status, string? response, string message, long duration)
    {
        if (!requestLogId.HasValue) return;
        // The request deadline may have elapsed; allow bounded cleanup to close its audit record.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5), _clock);
        await _audit.FailAsync(requestLogId.Value, status, response, message, duration, cleanup.Token);
    }

    private void EnsureSameTokenConnection()
    {
        if (!string.Equals(_tokens.ConnectionKey, ConnectionKey, StringComparison.Ordinal))
            throw new ProviderIntegrationException("The Blaaiz FX authentication connection changed. Revalidate the provider configuration before requesting a rate.");
    }
}
