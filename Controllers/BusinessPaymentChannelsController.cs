using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Dtos.Payments;
using KorridorX.Dtos.Wallets;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/business/payment-channels")]
public sealed class BusinessPaymentChannelsController(ConsumerWalletFundingService funding,
    WalletDepositAccountService accounts, PaymentChannelService channels,
    IBusinessAccessService access, AppDbContext db) : ControllerBase
{
    [HttpGet("wallets/{walletId:guid}/methods")]
    public async Task<IActionResult> Methods(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await funding.GetBusinessFundingMethodsAsync(Actor(), walletId, ct), "Available deposit channels retrieved."));

    [HttpPost("wallets/{walletId:guid}/collections")]
    public async Task<IActionResult> Create(Guid walletId, [FromBody] CreateConsumerWalletFundingCollectionRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await funding.CreateBusinessCollectionAsync(Actor(), walletId, request, ct), "Funding collection created."));

    [HttpPost("wallets/{walletId:guid}/collections/{collectionId:guid}/initiate")]
    public async Task<IActionResult> Initiate(Guid walletId, Guid collectionId, [FromBody] InitiateCollectionRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await funding.InitiateBusinessCollectionAsync(Actor(), walletId, collectionId, request, ct), "Funding collection initiated."));

    [HttpGet("wallets/{walletId:guid}/collections/{collectionId:guid}")]
    public async Task<IActionResult> Collection(Guid walletId, Guid collectionId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await funding.GetBusinessCollectionAsync(Actor(), walletId, collectionId, ct), "Funding collection retrieved."));

    [HttpGet("wallets/{walletId:guid}/bank-account")]
    public async Task<IActionResult> Account(Guid walletId, [FromQuery] Guid channelId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await accounts.GetAsync(Actor(), walletId, channelId, true, false, ct), "Deposit account retrieved."));

    [HttpPost("wallets/{walletId:guid}/bank-account")]
    public async Task<IActionResult> Provision(Guid walletId, [FromBody] CreateWalletDepositAccountRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await accounts.ProvisionAsync(Actor(), walletId, request.ChannelId, true, ct), "Deposit account setup processed."));

    [HttpPost("wallets/{walletId:guid}/bank-account/refresh")]
    public async Task<IActionResult> Refresh(Guid walletId, [FromBody] CreateWalletDepositAccountRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await accounts.GetAsync(Actor(), walletId, request.ChannelId, true, true, ct), "Deposit account status checked."));

    [HttpGet("transfers/{transferId:guid}/methods")]
    public async Task<IActionResult> TransferMethods(Guid transferId, CancellationToken ct)
    {
        var context = await access.EnsurePermissionAsync(Actor(), BusinessPermission.ViewTransfers | BusinessPermission.ManageFunding, ct);
        var transfer = await db.Transfers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == transferId && x.BusinessProfileId == context.BusinessProfileId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Transfer not found.");
        return Ok(ApiResponses.Ok(await channels.DepositMethodsAsync(transfer.SourceCountryCode, transfer.SourceCurrencyCode, true, false, null, ct), "Available transfer funding channels retrieved."));
    }

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
