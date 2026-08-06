using KorridorX.Dtos.Recipients;
using KorridorX.Infrastructure;
using KorridorX.Services.Recipients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/recipients")]
public class RecipientsController : ControllerBase
{
    private readonly IRecipientService _recipientService;

    public RecipientsController(IRecipientService recipientService)
    {
        _recipientService = recipientService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecipients(
        [FromQuery] string? search,
        [FromQuery] string? countryCode,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var result = await _recipientService.GetRecipientsAsync(userId, search, countryCode, includeInactive, page, pageSize, ct);

        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Recipients retrieved successfully."));
    }

    [HttpGet("{recipientId:guid}")]
    public async Task<IActionResult> GetRecipient(Guid recipientId, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _recipientService.GetRecipientAsync(userId, recipientId, ct);

        return Ok(ApiResponses.Ok(result, "Recipient retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRecipient(CreateRecipientRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _recipientService.CreateRecipientAsync(userId, request, ct);

        return Ok(ApiResponses.Ok(result, "Recipient created successfully."));
    }

    [HttpPut("{recipientId:guid}")]
    public async Task<IActionResult> UpdateRecipient(Guid recipientId, UpdateRecipientRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _recipientService.UpdateRecipientAsync(userId, recipientId, request, ct);

        return Ok(ApiResponses.Ok(result, "Recipient updated successfully."));
    }

    [HttpDelete("{recipientId:guid}")]
    public async Task<IActionResult> DeleteRecipient(Guid recipientId, CancellationToken ct)
    {
        var userId = GetUserId();
        await _recipientService.DeleteRecipientAsync(userId, recipientId, ct);

        return Ok(ApiResponses.Ok(new { recipientId }, "Recipient deleted successfully."));
    }

    [HttpPost("{recipientId:guid}/bank-accounts")]
    public async Task<IActionResult> AddBankAccount(Guid recipientId, AddRecipientBankAccountRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _recipientService.AddBankAccountAsync(userId, recipientId, request, ct);

        return Ok(ApiResponses.Ok(result, "Recipient bank account added successfully."));
    }

    [HttpPut("{recipientId:guid}/bank-accounts/{bankAccountId:guid}")]
    public async Task<IActionResult> UpdateBankAccount(Guid recipientId, Guid bankAccountId, UpdateRecipientBankAccountRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _recipientService.UpdateBankAccountAsync(userId, recipientId, bankAccountId, request, ct);

        return Ok(ApiResponses.Ok(result, "Recipient bank account updated successfully."));
    }

    [HttpDelete("{recipientId:guid}/bank-accounts/{bankAccountId:guid}")]
    public async Task<IActionResult> DeleteBankAccount(Guid recipientId, Guid bankAccountId, CancellationToken ct)
    {
        var userId = GetUserId();
        await _recipientService.DeleteBankAccountAsync(userId, recipientId, bankAccountId, ct);

        return Ok(ApiResponses.Ok(new { recipientId, bankAccountId }, "Recipient bank account deleted successfully."));
    }

    [HttpPost("{recipientId:guid}/mobile-wallets")]
    public async Task<IActionResult> AddMobileWallet(Guid recipientId, AddRecipientMobileWalletRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _recipientService.AddMobileWalletAsync(userId, recipientId, request, ct);

        return Ok(ApiResponses.Ok(result, "Recipient mobile wallet added successfully."));
    }

    [HttpPut("{recipientId:guid}/mobile-wallets/{mobileWalletId:guid}")]
    public async Task<IActionResult> UpdateMobileWallet(Guid recipientId, Guid mobileWalletId, UpdateRecipientMobileWalletRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _recipientService.UpdateMobileWalletAsync(userId, recipientId, mobileWalletId, request, ct);

        return Ok(ApiResponses.Ok(result, "Recipient mobile wallet updated successfully."));
    }

    [HttpDelete("{recipientId:guid}/mobile-wallets/{mobileWalletId:guid}")]
    public async Task<IActionResult> DeleteMobileWallet(Guid recipientId, Guid mobileWalletId, CancellationToken ct)
    {
        var userId = GetUserId();
        await _recipientService.DeleteMobileWalletAsync(userId, recipientId, mobileWalletId, ct);

        return Ok(ApiResponses.Ok(new { recipientId, mobileWalletId }, "Recipient mobile wallet deleted successfully."));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
