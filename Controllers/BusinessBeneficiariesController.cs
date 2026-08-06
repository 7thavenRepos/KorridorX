using System.Security.Claims;
using KorridorX.Dtos.BusinessBeneficiaries;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessBeneficiaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/business-beneficiaries")]
public class BusinessBeneficiariesController : ControllerBase
{
    private readonly IBusinessBeneficiaryService _service;

    public BusinessBeneficiariesController(IBusinessBeneficiaryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? countryCode,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetAsync(
            GetUserId(), search, countryCode, includeInactive, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta,
            "Business beneficiaries retrieved successfully."));
    }

    [HttpGet("{beneficiaryId:guid}")]
    public async Task<IActionResult> Get(Guid beneficiaryId, CancellationToken ct)
    {
        var result = await _service.GetAsync(GetUserId(), beneficiaryId, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessBeneficiaryRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary created successfully."));
    }

    [HttpPut("{beneficiaryId:guid}")]
    public async Task<IActionResult> Update(
        Guid beneficiaryId,
        [FromBody] UpdateBusinessBeneficiaryRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(GetUserId(), beneficiaryId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary updated successfully."));
    }

    [HttpDelete("{beneficiaryId:guid}")]
    public async Task<IActionResult> Delete(Guid beneficiaryId, CancellationToken ct)
    {
        await _service.DeleteAsync(GetUserId(), beneficiaryId, ct);
        return Ok(ApiResponses.Ok(new { beneficiaryId }, "Business beneficiary deleted successfully."));
    }

    [HttpPost("{beneficiaryId:guid}/bank-accounts")]
    public async Task<IActionResult> AddBankAccount(
        Guid beneficiaryId,
        [FromBody] AddBusinessBeneficiaryBankAccountRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.AddBankAccountAsync(GetUserId(), beneficiaryId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary bank account added successfully."));
    }

    [HttpPut("{beneficiaryId:guid}/bank-accounts/{bankAccountId:guid}")]
    public async Task<IActionResult> UpdateBankAccount(
        Guid beneficiaryId,
        Guid bankAccountId,
        [FromBody] UpdateBusinessBeneficiaryBankAccountRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateBankAccountAsync(
            GetUserId(), beneficiaryId, bankAccountId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary bank account updated successfully."));
    }

    [HttpPost("{beneficiaryId:guid}/bank-accounts/{bankAccountId:guid}/verify")]
    public async Task<IActionResult> VerifyBankAccount(
        Guid beneficiaryId,
        Guid bankAccountId,
        CancellationToken ct)
    {
        var result = await _service.VerifyBankAccountAsync(
            GetUserId(), beneficiaryId, bankAccountId, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary bank account verified successfully."));
    }

    [HttpDelete("{beneficiaryId:guid}/bank-accounts/{bankAccountId:guid}")]
    public async Task<IActionResult> DeleteBankAccount(
        Guid beneficiaryId,
        Guid bankAccountId,
        CancellationToken ct)
    {
        await _service.DeleteBankAccountAsync(GetUserId(), beneficiaryId, bankAccountId, ct);
        return Ok(ApiResponses.Ok(new { beneficiaryId, bankAccountId },
            "Business beneficiary bank account deleted successfully."));
    }

    [HttpPost("{beneficiaryId:guid}/mobile-wallets")]
    public async Task<IActionResult> AddMobileWallet(
        Guid beneficiaryId,
        [FromBody] AddBusinessBeneficiaryMobileWalletRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.AddMobileWalletAsync(GetUserId(), beneficiaryId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary mobile wallet added successfully."));
    }

    [HttpPut("{beneficiaryId:guid}/mobile-wallets/{mobileWalletId:guid}")]
    public async Task<IActionResult> UpdateMobileWallet(
        Guid beneficiaryId,
        Guid mobileWalletId,
        [FromBody] UpdateBusinessBeneficiaryMobileWalletRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateMobileWalletAsync(
            GetUserId(), beneficiaryId, mobileWalletId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business beneficiary mobile wallet updated successfully."));
    }

    [HttpDelete("{beneficiaryId:guid}/mobile-wallets/{mobileWalletId:guid}")]
    public async Task<IActionResult> DeleteMobileWallet(
        Guid beneficiaryId,
        Guid mobileWalletId,
        CancellationToken ct)
    {
        await _service.DeleteMobileWalletAsync(GetUserId(), beneficiaryId, mobileWalletId, ct);
        return Ok(ApiResponses.Ok(new { beneficiaryId, mobileWalletId },
            "Business beneficiary mobile wallet deleted successfully."));
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
