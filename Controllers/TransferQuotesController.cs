using System.Security.Claims;
using KorridorX.Dtos.Fx;
using KorridorX.Infrastructure;
using KorridorX.Services.Fx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/transfer-quotes")]
public class TransferQuotesController : ControllerBase
{
    private readonly ITransferQuoteService _quoteService;

    public TransferQuotesController(ITransferQuoteService quoteService)
    {
        _quoteService = quoteService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateTransferQuoteRequestDto request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _quoteService.CreateQuoteAsync(userId, request, ct);

        return Ok(ApiResponses.Ok(
            result,
            "Transfer quote created successfully."));
    }

    [HttpGet("{quoteId:guid}")]
    public async Task<IActionResult> GetQuoteById(
        [FromRoute] Guid quoteId,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _quoteService.GetQuoteByIdAsync(userId, quoteId, ct);

        return Ok(ApiResponses.Ok(
            result,
            "Transfer quote retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}