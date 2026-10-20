using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Wallets;
using KorridorX.Infrastructure;
using KorridorX.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Consumer")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/wallets")]
public sealed class WalletsController : ControllerBase
{
    private readonly IConsumerWalletService _wallets;
    private readonly IConsumerWalletFundingService _funding;
    private readonly IConsumerWalletReceivingAccountService _receivingAccounts;
    private readonly ILogger<WalletsController> _logger;

    public WalletsController(
        IConsumerWalletService wallets,
        IConsumerWalletFundingService funding,
        IConsumerWalletReceivingAccountService receivingAccounts,
        ILogger<WalletsController> logger)
    {
        _wallets = wallets;
        _funding = funding;
        _receivingAccounts = receivingAccounts;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetWallets(CancellationToken ct)
    {
        var result = await _wallets.GetWalletsAsync(GetUserId(), ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallets retrieved successfully."));
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableWalletAssets(CancellationToken ct)
    {
        var result = await _wallets.GetAvailableAssetsAsync(GetUserId(), ct);

        return Ok(ApiResponses.Ok(
            result,
            "Available wallet assets retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateWallet(
        [FromBody] CreateConsumerWalletRequestDto request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _wallets.CreateWalletAsync(userId, request, ct);
        try
        {
            result = result with { ReceivingAccount = await _receivingAccounts.SetupAsync(userId, result.Id, ct) };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            // Adding the wallet has already committed. A separate provider/setup failure must
            // not report that the wallet creation failed or invite another financial account.
            _logger.LogWarning("Wallet {WalletId} was added but receiving setup needs attention; error type {ErrorType}.",
                result.Id, error.GetType().Name);
            result = result with { ReceivingAccount = new("ActionRequired",
                "Your wallet was added. Receiving-account setup could not be confirmed. Open Add money to check its status or contact support.", null, null, true) };
        }

        return Ok(ApiResponses.Ok(
            result,
            "Wallet is ready."));
    }

    [HttpGet("{walletId:guid}/receiving-account")]
    public async Task<IActionResult> GetReceivingAccount(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _receivingAccounts.GetAsync(GetUserId(), walletId, ct), "Receiving-account status retrieved."));

    [HttpPost("{walletId:guid}/receiving-account/setup")]
    public async Task<IActionResult> SetupReceivingAccount(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await _receivingAccounts.SetupAsync(GetUserId(), walletId, ct), "Receiving-account setup checked."));

    [HttpGet("{walletId:guid}/funding-methods")]
    public async Task<IActionResult> GetFundingMethods(
        [FromRoute] Guid walletId,
        CancellationToken ct)
    {
        var result = await _funding.GetFundingMethodsAsync(GetUserId(), walletId, ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding methods retrieved successfully."));
    }

    [HttpPost("{walletId:guid}/collections")]
    public async Task<IActionResult> CreateFundingCollection(
        [FromRoute] Guid walletId,
        [FromBody] CreateConsumerWalletFundingCollectionRequestDto request,
        CancellationToken ct)
    {
        var result = await _funding.CreateCollectionAsync(
            GetUserId(),
            walletId,
            request,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding collection created successfully."));
    }

    [HttpPost("{walletId:guid}/collections/{collectionId:guid}/initiate")]
    public async Task<IActionResult> InitiateFundingCollection(
        [FromRoute] Guid walletId,
        [FromRoute] Guid collectionId,
        [FromBody] KorridorX.Dtos.Payments.InitiateCollectionRequestDto request,
        CancellationToken ct)
    {
        var result = await _funding.InitiateCollectionAsync(
            GetUserId(),
            walletId,
            collectionId,
            request,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding collection initiated successfully."));
    }

    [HttpGet("{walletId:guid}/collections/{collectionId:guid}")]
    public async Task<IActionResult> GetFundingCollection(
        [FromRoute] Guid walletId,
        [FromRoute] Guid collectionId,
        CancellationToken ct)
    {
        var result = await _funding.GetCollectionAsync(
            GetUserId(),
            walletId,
            collectionId,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Wallet funding collection retrieved successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
