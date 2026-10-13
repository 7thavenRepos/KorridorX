using KorridorX.Data;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.SearchHelp;
using Microsoft.EntityFrameworkCore;
namespace KorridorX.Tests.SearchHelp;
internal static class SearchHelpTestScopes
{
    internal static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid BusinessId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static SearchScope Admin(params string[] roles) => new("admin",User,roles.ToHashSet(),null);
    internal static SearchScope Business(BusinessPermission permissions=BusinessPermission.All, bool owner=false) =>
        new("business",User,new HashSet<string>{"Business"},new BusinessAccessContext(BusinessId,User,
            owner?BusinessUserRole.Owner:BusinessUserRole.Member,permissions,owner,false,1,null,false));
    // Translation-only tests. The invalid loopback port prevents accidental real database access.
    internal static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=1;Database=searchhelp_translation_only;Username=not_used;Password=not_used").Options);
}
