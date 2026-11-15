using KorridorX.Dtos.AdminFunding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KorridorX.Tests;

public sealed class AdminWalletFundingRequestValidationTests
{
    [Fact]
    public void Create_accepts_valid_funding_reason()
    {
        var modelState = ValidateWithMvc(CreateRequest("adjustment"));

        Assert.True(modelState.IsValid);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_or_blank_reason(string? reason)
    {
        var modelState = ValidateWithMvc(CreateRequest(reason));

        AssertReasonError(modelState);
    }

    [Fact]
    public void Create_accepts_reason_at_1000_character_limit()
    {
        var modelState = ValidateWithMvc(CreateRequest(new string('a', 1000)));

        Assert.True(modelState.IsValid);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void Create_rejects_reason_over_1000_character_limit()
    {
        var modelState = ValidateWithMvc(CreateRequest(new string('a', 1001)));

        AssertReasonError(modelState);
    }

    [Theory]
    [InlineData("Reviewed funding request")]
    [InlineData(null)]
    [InlineData("")]
    public void Review_preserves_optional_reason_validation_contract(string? reason)
    {
        // The rejection service separately requires a reason. This DTO's existing
        // validation contract only limits its length and must remain unchanged.
        var modelState = ValidateWithMvc(new ReviewAdminWalletFundingRequestDto(reason));

        Assert.True(modelState.IsValid);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void Review_accepts_reason_at_1000_character_limit()
    {
        var modelState = ValidateWithMvc(new ReviewAdminWalletFundingRequestDto(new string('a', 1000)));

        Assert.True(modelState.IsValid);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void Review_rejects_reason_over_1000_character_limit()
    {
        var modelState = ValidateWithMvc(new ReviewAdminWalletFundingRequestDto(new string('a', 1001)));

        AssertReasonError(modelState);
    }

    private static CreateAdminWalletFundingRequestDto CreateRequest(string? reason) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        15000m,
        reason!);

    private static ModelStateDictionary ValidateWithMvc(object model)
    {
        // Use the real MVC metadata/validation path: property-only DataAnnotations
        // validation would miss the positional-record metadata exception.
        // This isolated container never starts the application or a database fixture.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();

        var context = new ActionContext(
            new DefaultHttpContext { RequestServices = provider },
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        provider.GetRequiredService<IObjectModelValidator>().Validate(
            context, validationState: null, prefix: string.Empty, model: model);

        return context.ModelState;
    }

    private static void AssertReasonError(ModelStateDictionary modelState)
    {
        Assert.False(modelState.IsValid);
        Assert.Equal(1, modelState.ErrorCount);
        Assert.True(modelState.TryGetValue(nameof(CreateAdminWalletFundingRequestDto.Reason), out var reasonState));
        Assert.NotNull(reasonState);
        Assert.Single(reasonState.Errors);
    }
}
