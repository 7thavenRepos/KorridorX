using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Dtos.Instant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Tests;

public sealed class InstantWalletExecutionContractTests
{
    [Fact]
    public void ConsumerInstantExecution_RequiresBodyPinAndRateLimit()
    {
        var action = typeof(InstantTradingController).GetMethod(nameof(InstantTradingController.ExecuteQuote));
        Assert.NotNull(action);
        Assert.Contains(action!.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ExecuteInstantQuoteRequestDto) &&
            parameter.GetCustomAttribute<FromBodyAttribute>() is not null);
        Assert.NotNull(action.GetCustomAttribute<EnableRateLimitingAttribute>());
        Assert.Equal(typeof(string),
            typeof(ExecuteInstantQuoteRequestDto)
                .GetProperty(nameof(ExecuteInstantQuoteRequestDto.TransactionPin))?.PropertyType);
    }
}
