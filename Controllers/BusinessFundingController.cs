using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.BusinessFunding;
using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessFunding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/business/funding")]
public class BusinessFundingController : ControllerBase
{
    private readonly IBusinessFundingService _service;

    public BusinessFundingController(IBusinessFundingService service)
    {
        _service = service;
    }

    [HttpGet("wallets")]
    public async Task<IActionResult> GetWallets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetWalletsAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business wallets retrieved successfully."));
    }

    [HttpGet("wallets/{walletId:guid}/ledger")]
    public async Task<IActionResult> GetLedger(
        Guid walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetLedgerAsync(GetUserId(), walletId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business wallet ledger retrieved successfully."));
    }

    [HttpGet("transfers/{transferId:guid}")]
    public async Task<IActionResult> GetTransferFunding(Guid transferId, CancellationToken ct)
    {
        var result = await _service.GetTransferFundingAsync(GetUserId(), transferId, ct);
        return Ok(ApiResponses.Ok(result, "Business transfer funding retrieved successfully."));
    }

    [HttpPost("transfers/{transferId:guid}/wallet")]
    public async Task<IActionResult> FundFromWallet(Guid transferId, CancellationToken ct)
    {
        var result = await _service.FundTransferFromWalletAsync(GetUserId(), transferId, ct);
        return Ok(ApiResponses.Ok(result, "Business-wallet funding reserved successfully."));
    }

    [HttpPost("transfers/{transferId:guid}/collections")]
    public async Task<IActionResult> CreateExternalCollection(
        Guid transferId,
        [FromBody] CreateBusinessExternalCollectionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.CreateExternalCollectionAsync(GetUserId(), transferId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business external collection created successfully."));
    }

    [HttpPost("collections/{collectionId:guid}/initiate")]
    public async Task<IActionResult> InitiateExternalCollection(
        Guid collectionId,
        [FromBody] InitiateCollectionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.InitiateExternalCollectionAsync(GetUserId(), collectionId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business external collection initiated successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
