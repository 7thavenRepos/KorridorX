using KorridorX.Infrastructure;
using KorridorX.Services.SearchHelp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace KorridorX.Controllers;
[ApiController, Authorize, EnableRateLimiting("WorkspaceSearch")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[Route("api/{portal}/workspace-search")]
public sealed class WorkspaceSearchController(SearchScopeResolver scopes,WorkspaceSearchService search,HelpCatalog help) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Find(string portal,[FromQuery] string? q,[FromQuery] string? category="all",[FromQuery] int page=1,[FromQuery] int pageSize=15,CancellationToken ct=default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try { return Ok(ApiResponses.Ok(await search.SearchAsync(await scopes.ResolveAsync(User,portal,timeout.Token),q,category,page,pageSize,timeout.Token))); }
        catch (ArgumentException ex) { return BadRequest(ApiResponses.Fail(ex.Message,"INVALID_SEARCH")); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(503,ApiResponses.Fail("Search took too long. Try an exact reference or select a category.","SEARCH_TIMEOUT")); }
    }
    [HttpGet("records/{category}/{key}")]
    public async Task<IActionResult> Record(string portal,string category,string key,CancellationToken ct)
    {
        var item=await search.RecordAsync(await scopes.ResolveAsync(User,portal,ct),category,key,ct);
        return item is null ? NotFound(ApiResponses.Fail("Record not found or unavailable to this account.","NOT_FOUND")) : Ok(ApiResponses.Ok(item));
    }
    [HttpGet("guides")]
    public async Task<IActionResult> Guides(string portal,[FromQuery] string? pageRoute,CancellationToken ct)
    {
        if (pageRoute?.Length>250) return BadRequest(ApiResponses.Fail("Page path is too long."));
        return Ok(ApiResponses.Ok(help.Library(await scopes.ResolveAsync(User,portal,ct),pageRoute)));
    }
    [HttpGet("guides/{slug}")]
    public async Task<IActionResult> Guide(string portal,string slug,CancellationToken ct)
    {
        var article=help.Article(await scopes.ResolveAsync(User,portal,ct),slug);
        return article is null ? NotFound(ApiResponses.Fail("Guide not found or unavailable to this account.","NOT_FOUND")) : Ok(ApiResponses.Ok(article));
    }
}
