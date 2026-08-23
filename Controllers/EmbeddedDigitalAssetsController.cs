using KorridorX.Dtos.DigitalAssets;
using KorridorX.Infrastructure;
using KorridorX.Services.DigitalAssets;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/v1/embedded/customers/{businessCustomerId:guid}/digital-assets")]
public sealed class EmbeddedDigitalAssetsController : ControllerBase
{
    private readonly IEmbeddedDigitalAssetService _service;
    public EmbeddedDigitalAssetsController(IEmbeddedDigitalAssetService service) => _service = service;

    [HttpGet("deposit-addresses")]
    public async Task<IActionResult> DepositAddresses(Guid businessCustomerId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetDepositAddressesAsync(businessCustomerId, ct), "Digital-asset deposit addresses retrieved successfully."));

    [HttpPost("deposit-addresses")]
    public async Task<IActionResult> CreateDepositAddress(Guid businessCustomerId, [FromBody] CreateDigitalAssetDepositAddressRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.CreateDepositAddressAsync(businessCustomerId, request, ct), "Digital-asset deposit address created successfully."));

    [HttpGet("withdrawal-destinations")]
    public async Task<IActionResult> WithdrawalDestinations(Guid businessCustomerId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetWithdrawalDestinationsAsync(businessCustomerId, ct), "Digital-asset withdrawal destinations retrieved successfully."));

    [HttpPost("withdrawal-destinations")]
    public async Task<IActionResult> CreateWithdrawalDestination(Guid businessCustomerId, [FromBody] CreateDigitalAssetWithdrawalDestinationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.CreateWithdrawalDestinationAsync(businessCustomerId, request, ct), "Digital-asset withdrawal destination created successfully."));

    [HttpGet("withdrawals")]
    public async Task<IActionResult> Withdrawals(Guid businessCustomerId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.GetWithdrawalsAsync(businessCustomerId, ct), "Digital-asset withdrawals retrieved successfully."));

    [HttpPost("withdrawals")]
    public async Task<IActionResult> CreateWithdrawal(Guid businessCustomerId, [FromBody] CreateDigitalAssetWithdrawalRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _service.CreateWithdrawalAsync(businessCustomerId, request, ct), "Digital-asset withdrawal submitted successfully."));
}
