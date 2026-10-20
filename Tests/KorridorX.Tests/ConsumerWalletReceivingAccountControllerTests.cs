using System.Security.Claims;
using KorridorX.Controllers;
using KorridorX.Dtos.Wallets;
using KorridorX.Infrastructure;
using KorridorX.Services.Wallets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace KorridorX.Tests;

public sealed class ConsumerWalletReceivingAccountControllerTests
{
    [Theory]
    [InlineData("Ready")]
    [InlineData("Pending")]
    [InlineData("VerificationRequired")]
    [InlineData("Unavailable")]
    public async Task Explicit_wallet_add_starts_receiving_setup_for_the_authenticated_owner_and_reports_its_real_status(string status)
    {
        var user = Guid.NewGuid(); var order = new List<string>();
        var wallets = new Wallets { OnCreate = () => order.Add("wallet-saved") };
        var receiving = new Receiving { Status = status, OnSetup = () => order.Add("receiving-setup") };
        var controller = Controller(user, wallets, receiving);
        var response = Assert.IsType<OkObjectResult>(await controller.CreateWallet(new("GBP"), default));
        var data = Assert.IsType<ApiResponse<ConsumerWalletDto>>(response.Value).Data!;
        Assert.Equal(["wallet-saved", "receiving-setup"], order);
        Assert.Equal(wallets.Wallet.Id, data.Id);
        Assert.Equal(status, data.ReceivingAccount!.Status);
        Assert.Equal((user, data.Id), Assert.Single(receiving.SetupCalls));
        Assert.Equal(0m, data.AvailableBalance);
    }

    [Fact]
    public async Task Receiving_setup_failure_does_not_turn_a_saved_wallet_into_a_failed_add()
    {
        var wallets = new Wallets();
        var receiving = new Receiving { OnSetup = () => throw new InvalidOperationException("provider unavailable") };
        var response = Assert.IsType<OkObjectResult>(await Controller(Guid.NewGuid(), wallets, receiving).CreateWallet(new("GBP"), default));
        var data = Assert.IsType<ApiResponse<ConsumerWalletDto>>(response.Value).Data!;
        Assert.Equal(wallets.Wallet.Id, data.Id);
        Assert.Equal("ActionRequired", data.ReceivingAccount!.Status);
        Assert.Contains("wallet was added", data.ReceivingAccount.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, wallets.Creates);
        Assert.Single(receiving.SetupCalls);
    }

    [Fact]
    public async Task Rejected_wallet_add_never_attempts_receiving_setup()
    {
        var receiving = new Receiving();
        var wallets = new Wallets { OnCreate = () => throw new InvalidOperationException("unsupported wallet") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Controller(Guid.NewGuid(), wallets, receiving).CreateWallet(new("ZZZ"), default));
        Assert.Empty(receiving.SetupCalls);
    }

    [Fact]
    public async Task Read_only_status_does_not_initiate_account_setup()
    {
        var user = Guid.NewGuid(); var id = Guid.NewGuid(); var receiving = new Receiving();
        await Controller(user, new Wallets(), receiving).GetReceivingAccount(id, default);
        Assert.Equal((user, id), Assert.Single(receiving.ReadCalls));
        Assert.Empty(receiving.SetupCalls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_successful_setup()
    {
        using var cancel = new CancellationTokenSource();
        var receiving = new Receiving { OnSetup = () => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Controller(Guid.NewGuid(), new Wallets(), receiving).CreateWallet(new("GBP"), cancel.Token));
    }

    private static WalletsController Controller(Guid user, Wallets wallets, Receiving receiving) =>
        new(wallets, null!, receiving, NullLogger<WalletsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "test")) }
            }
        };

    private sealed class Wallets : IConsumerWalletService
    {
        public ConsumerWalletDto Wallet { get; } = new(Guid.NewGuid(), "GBP", "Pounds", "GBP", 2, "Active", 0m, 0m, 0m,
            false, true, true, true, true, false, false, DateTime.UtcNow);
        public Action? OnCreate { get; init; }
        public int Creates { get; private set; }
        public Task<ConsumerWalletDto> CreateWalletAsync(Guid userId, CreateConsumerWalletRequestDto request, CancellationToken ct = default)
        { Creates++; OnCreate?.Invoke(); return Task.FromResult(Wallet); }
        public Task<IReadOnlyList<ConsumerWalletDto>> GetWalletsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AvailableConsumerWalletAssetDto>> GetAvailableAssetsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Receiving : IConsumerWalletReceivingAccountService
    {
        public string Status { get; init; } = "Pending";
        public Action? OnSetup { get; init; }
        public List<(Guid UserId, Guid WalletId)> SetupCalls { get; } = [];
        public List<(Guid UserId, Guid WalletId)> ReadCalls { get; } = [];
        public Task<ConsumerWalletReceivingAccountDto> GetAsync(Guid userId, Guid walletId, CancellationToken ct = default)
        { ReadCalls.Add((userId, walletId)); return Task.FromResult(new ConsumerWalletReceivingAccountDto(Status, "Read status", null, null, false)); }
        public Task<ConsumerWalletReceivingAccountDto> SetupAsync(Guid userId, Guid walletId, CancellationToken ct = default)
        { SetupCalls.Add((userId, walletId)); OnSetup?.Invoke(); return Task.FromResult(new ConsumerWalletReceivingAccountDto(Status, "Setup status", null, null, false)); }
    }
}
