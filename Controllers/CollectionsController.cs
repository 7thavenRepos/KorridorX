using System.Security.Claims;
using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;
using KorridorX.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/collections")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CollectionsController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    [HttpGet("~/api/transfers/{transferId:guid}/collection-methods")]
    public async Task<IActionResult> GetAvailablePaymentMethods(
        [FromRoute] Guid transferId,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _collectionService.GetAvailablePaymentMethodsAsync(
            userId,
            transferId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Available collection payment methods retrieved successfully."));
    }

    [HttpPost("~/api/transfers/{transferId:guid}/collections")]
    public async Task<IActionResult> CreateCollection(
        [FromRoute] Guid transferId,
        [FromBody] CreateCollectionRequestDto request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _collectionService.CreateCollectionAsync(
            userId,
            transferId,
            request,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Collection created successfully."));
    }

    [HttpGet("{collectionId:guid}")]
    public async Task<IActionResult> GetCollectionById(
        [FromRoute] Guid collectionId,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _collectionService.GetCollectionByIdAsync(
            userId,
            collectionId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Collection retrieved successfully."));
    }

    [HttpGet("~/api/transfers/{transferId:guid}/collection")]
    public async Task<IActionResult> GetTransferCollection(
        [FromRoute] Guid transferId,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _collectionService.GetTransferCollectionAsync(
            userId,
            transferId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Transfer collection retrieved successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetMyCollections(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        var result = await _collectionService.GetMyCollectionsAsync(
            userId,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Collections retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
