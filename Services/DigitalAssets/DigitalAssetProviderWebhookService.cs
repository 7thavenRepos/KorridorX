using System.Security.Cryptography;
using System.Text;
using KorridorX.Data;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Providers.DigitalAssets;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.DigitalAssets;

public sealed class DigitalAssetProviderWebhookService :
    IDigitalAssetProviderWebhookService
{
    private readonly AppDbContext _db;
    private readonly IDigitalAssetProviderRegistry _registry;
    private readonly DigitalAssetProviderOperationsService _operations;
    private readonly IDigitalAssetSettlementService _settlement;

    public DigitalAssetProviderWebhookService(
        AppDbContext db,
        IDigitalAssetProviderRegistry registry,
        IDigitalAssetProviderOperationsService operations,
        IDigitalAssetSettlementService settlement)
    {
        _db = db;
        _registry = registry;
        _operations = operations as DigitalAssetProviderOperationsService
            ?? throw new InvalidOperationException(
                "DigitalAssetProviderOperationsService registration is required.");
        _settlement = settlement;
    }

    public async Task ProcessAsync(
        string providerCode,
        string payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default)
    {
        var config = await _operations.GetActiveConfigurationAsync(providerCode, ct);

        if (!config.WebhooksEnabled)
            throw new InvalidOperationException("Digital-asset provider webhooks are disabled.");

        var provider = _registry.GetRequired(config.ProviderCode);
        if (provider is not IDigitalAssetWebhookProvider webhook)
            throw new InvalidOperationException(
                $"Digital-asset provider '{config.ProviderCode}' does not support webhooks.");

        var secret = _operations.UnprotectWebhookSecret(config);

        if (!webhook.VerifyWebhookSignature(payload, headers, secret))
            throw new UnauthorizedAccessException("Digital-asset webhook signature is invalid.");

        var evt = await webhook.ParseWebhookAsync(payload, headers, ct);

        if (string.IsNullOrWhiteSpace(evt.ProviderEventId))
            throw new InvalidOperationException("Provider webhook event identifier is required.");

        var payloadHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

        var receipt = await _db.DigitalAssetWebhookReceipts.FirstOrDefaultAsync(x =>
            x.ProviderCode == config.ProviderCode &&
            x.ProviderEventId == evt.ProviderEventId,
            ct);

        if (receipt is not null)
        {
            if (!string.Equals(receipt.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Provider webhook replay used the same event ID with a different payload.");

            if (receipt.Status == DigitalAssetWebhookReceiptStatus.Processed)
                return;
        }
        else
        {
            receipt = new DigitalAssetWebhookReceipt
            {
                ProviderCode = config.ProviderCode,
                ProviderEventId = evt.ProviderEventId.Trim(),
                PayloadHash = payloadHash,
                EventType = evt.EventType,
                Status = DigitalAssetWebhookReceiptStatus.Received,
                ReceivedAt = DateTime.UtcNow
            };
            _db.DigitalAssetWebhookReceipts.Add(receipt);
            await _db.SaveChangesAsync(ct);
        }

        try
        {
            if (evt.Inbound is not null)
                await _settlement.ProcessInboundAsync(evt.Inbound, ct);

            if (evt.Outbound is not null)
                await _settlement.ProcessOutboundAsync(evt.Outbound, ct);

            if (evt.Inbound is null && evt.Outbound is null)
                throw new InvalidOperationException(
                    "Digital-asset webhook did not contain a supported settlement event.");

            receipt.Status = DigitalAssetWebhookReceiptStatus.Processed;
            receipt.ProcessedAt = DateTime.UtcNow;
            receipt.ErrorMessage = null;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            receipt.Status = DigitalAssetWebhookReceiptStatus.Failed;
            receipt.ErrorMessage =
                ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }
}
