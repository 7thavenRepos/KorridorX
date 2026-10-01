using System.Reflection;
using System.Security.Claims;
using KorridorX.Controllers;
using KorridorX.Dtos.Wallets;
using KorridorX.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Tests;

public sealed class ConsumerWalletValuationControllerTests
{
    [Fact]
    public async Task Ownership_is_taken_from_the_authenticated_claim_and_cancellation_is_forwarded()
    {
        var service = new Probe();
        var owner = Guid.NewGuid();
        var controller = Controller(service, owner.ToString());
        using var cancel = new CancellationTokenSource();
        var result = await controller.GetValuation("CAD", cancel.Token);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(owner, service.UserId);
        Assert.Equal("CAD", service.Currency);
        Assert.Equal(cancel.Token, service.Token);
    }

    [Fact]
    public async Task A_missing_or_invalid_identity_never_calls_the_valuation_service()
    {
        foreach (var claim in new string?[] { null, "invalid" })
        {
            var service = new Probe();
            var controller = Controller(service, claim);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetValuation("CAD", default));
            Assert.Equal(Guid.Empty, service.UserId);
        }
    }

    [Fact]
    public void Endpoint_is_a_non_cached_consumer_only_read_with_no_owner_input()
    {
        var type = typeof(ConsumerWalletValuationController);
        Assert.Equal("Consumer", Assert.Single(type.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
        var method = type.GetMethod(nameof(ConsumerWalletValuationController.GetValuation))!;
        Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.DoesNotContain(method.GetParameters(), x => x.Name == "userId" || x.Name == "ownerId");
        var cache = Assert.Single(method.GetCustomAttributes<ResponseCacheAttribute>());
        Assert.True(cache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }

    private static ConsumerWalletValuationController Controller(Probe service, string? owner)
    {
        var claims = owner is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, owner) };
        return new ConsumerWalletValuationController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
    }

    private sealed class Probe : IConsumerWalletValuationService
    {
        public Guid UserId { get; private set; }
        public string? Currency { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<ConsumerWalletValuationDto> GetValuationAsync(Guid userId, string baseAssetCode, CancellationToken ct = default)
        {
            UserId = userId; Currency = baseAssetCode; Token = ct;
            return Task.FromResult(new ConsumerWalletValuationDto("CAD", "Canadian Dollar", "$", 2,
                "Complete", 0m, DateTime.UtcNow, null, Array.Empty<ConsumerWalletValuationComponentDto>()));
        }
    }
}
