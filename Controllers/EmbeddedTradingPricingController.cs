using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/v1/embedded/trading/pricing-policies")]
public sealed class EmbeddedTradingPricingController : ControllerBase
{
    private readonly IEmbeddedTradingService _trading;

    public EmbeddedTradingPricingController(IEmbeddedTradingService trading)
    {
        _trading = trading;
    }

    [HttpGet]
    public async Task<IActionResult> Policies(CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.GetPricingPoliciesAsync(ct),
            "Business pricing policies retrieved successfully."));

    [HttpGet("performance")]
    public async Task<IActionResult> Performance(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.GetPricingPerformanceAsync(from, to, ct),
            "Business pricing performance retrieved successfully."));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessPricingPolicyRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.CreatePricingPolicyAsync(request, ct),
            "Business pricing policy created successfully."));

    [HttpPut("{policyId:guid}")]
    public async Task<IActionResult> Update(
        Guid policyId,
        [FromBody] UpdateBusinessPricingPolicyRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.UpdatePricingPolicyAsync(policyId, request, ct),
            "Business pricing policy updated successfully."));

    [HttpPost("{policyId:guid}/disable")]
    public async Task<IActionResult> Disable(
        Guid policyId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _trading.DisablePricingPolicyAsync(policyId, ct),
            "Business pricing policy disabled successfully."));
}
