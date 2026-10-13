using System.Security.Claims;
using KorridorX.Dtos.BusinessContext;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessContext;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.SearchHelp;
namespace KorridorX.Tests.SearchHelp;
public sealed class SearchScopeResolverTests
{
    private static ClaimsPrincipal User(params string[] roles) => new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,SearchHelpTestScopes.User.ToString())}.Concat(roles.Select(x=>new Claim(ClaimTypes.Role,x))),"test"));
    private sealed class Selected(Guid? id):IBusinessContextAccessor { public Guid? GetSelectedBusinessProfileId()=>id; }
    private sealed class Access:IBusinessAccessService
    {
        public BusinessAccessContext Context=SearchHelpTestScopes.Business().Business!;
        public IReadOnlyList<AvailableBusinessContextDto> Available=[];public bool Reject;public int Calls;
        public Task<BusinessAccessContext> GetAccessAsync(Guid id,CancellationToken ct=default) { Calls++;if(Reject)throw new ForbiddenException("No access.");return Task.FromResult(Context); }
        public Task<BusinessAccessContext> EnsurePermissionAsync(Guid id,BusinessPermission p,CancellationToken ct=default)=>GetAccessAsync(id,ct);
        public Task<IReadOnlyList<AvailableBusinessContextDto>> GetAvailableBusinessesAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Available);
    }
    [Fact] public async Task RejectsUnauthenticatedRequests()
    { await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new SearchScopeResolver(new Access(),new Selected(null)).ResolveAsync(new ClaimsPrincipal(),"admin",default)); }
    [Fact] public async Task BusinessIdentityCannotRequestAdminPortal()
    { await Assert.ThrowsAsync<ForbiddenException>(()=>new SearchScopeResolver(new Access(),new Selected(null)).ResolveAsync(User("Business"),"admin",default)); }
    [Fact] public async Task InvalidPortalDoesNotFallBackToBusiness()
    { await Assert.ThrowsAsync<ForbiddenException>(()=>new SearchScopeResolver(new Access(),new Selected(null)).ResolveAsync(User("Business"),"other",default)); }
    [Fact] public async Task ExplicitRevokedSelectionCannotBecomeOnboarding()
    { var access=new Access{Reject=true};await Assert.ThrowsAsync<ForbiddenException>(()=>new SearchScopeResolver(access,new Selected(Guid.NewGuid())).ResolveAsync(User("Business"),"business",default));Assert.Equal(1,access.Calls); }
    [Fact] public async Task NoAvailableBusinessCanReadOnboardingOnly()
    { var access=new Access();var scope=await new SearchScopeResolver(access,new Selected(null)).ResolveAsync(User("Business"),"business",default);Assert.Null(scope.Business);Assert.Equal(0,access.Calls); }
    [Fact] public async Task SelectedContextUsesExistingMembershipService()
    { var access=new Access();var scope=await new SearchScopeResolver(access,new Selected(SearchHelpTestScopes.BusinessId)).ResolveAsync(User("Business"),"business",default);Assert.Equal(access.Context,scope.Business);Assert.Equal(1,access.Calls); }
}
