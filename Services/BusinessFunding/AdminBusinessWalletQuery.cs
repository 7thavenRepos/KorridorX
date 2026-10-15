using KorridorX.Data;
using KorridorX.Dtos.BusinessFunding;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessFunding;

public static class AdminBusinessWalletQuery
{
    public static IQueryable<AdminBusinessWalletDto> Build(
        AppDbContext db,
        Guid? businessProfileId,
        string? search,
        string? currencyCode,
        FinancialAccountStatus? status)
    {
        var query =
            from account in db.FinancialAccounts.AsNoTracking()
            join business in db.BusinessProfiles.AsNoTracking()
                on account.OwnerId equals business.Id
            where account.OwnerType == FinancialAccountOwnerType.Business &&
                  !account.IsDeleted &&
                  !business.IsDeleted
            select new { account, business };

        if (businessProfileId.HasValue)
            query = query.Where(x => x.business.Id == businessProfileId.Value);

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var code = currencyCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.account.AssetCode == code);
        }

        if (status.HasValue)
            query = query.Where(x => x.account.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.business.BusinessName.ToLower().Contains(term) ||
                (x.business.TradingName != null && x.business.TradingName.ToLower().Contains(term)) ||
                (x.business.ContactEmail != null && x.business.ContactEmail.ToLower().Contains(term)));
        }

        return query
            .OrderBy(x => x.business.BusinessName)
            .ThenBy(x => x.account.AssetCode)
            .ThenBy(x => x.account.Id)
            .Select(x => new AdminBusinessWalletDto(
                x.account.Id,
                x.business.Id,
                x.business.BusinessName,
                x.business.TradingName,
                x.business.ContactEmail,
                x.business.CountryCode,
                x.account.AssetCode,
                x.account.Status,
                x.account.SettledBalance,
                x.account.AvailableBalance,
                x.account.HeldBalance,
                x.account.CreatedAt,
                x.account.LastUpdatedAt));
    }
}
