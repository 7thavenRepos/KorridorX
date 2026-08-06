using System.Security.Claims;
using KorridorX.Infrastructure;
using KorridorX.Services.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/lookups/banks")]
public class BankDirectoryController : ControllerBase
{
    private readonly IBankDirectoryService _bankDirectoryService;

    public BankDirectoryController(IBankDirectoryService bankDirectoryService)
    {
        _bankDirectoryService = bankDirectoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetBanks(
        [FromQuery] string? countryCode,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var canIncludeInactive = includeInactive &&
            (User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("Operations"));

        var result = await _bankDirectoryService.GetBanksAsync(
            countryCode,
            search,
            canIncludeInactive,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Banks retrieved successfully."));
    }

    [HttpPost("/api/recipients/{recipientId:guid}/bank-accounts/{bankAccountId:guid}/verify")]
    public async Task<IActionResult> VerifyRecipientBankAccount(
        Guid recipientId,
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        var result = await _bankDirectoryService.VerifyRecipientBankAccountAsync(
            GetUserId(),
            recipientId,
            bankAccountId,
            ct);

        return Ok(ApiResponses.Ok(result, "Recipient bank account verified successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
