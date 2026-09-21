using System.Security.Cryptography;
using System.Text;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Providers;

public interface IProviderWalletResolver
{
    string EnvironmentName { get; }
    string ConnectionKey { get; }
    Task<string> SelectAsync(string operationType, Guid operationId, string providerCode,
        string assetCode, string? networkCode, string purpose, bool previouslySubmitted, CancellationToken ct = default);
}

public sealed class ProviderWalletResolver : IProviderWalletResolver
{
    public const string Collection = "COLLECTION";
    public const string Payout = "PAYOUT";
    private readonly DbContextOptions<AppDbContext> _selectionOptions;
    public string EnvironmentName { get; }
    public string ConnectionKey { get; }

    public ProviderWalletResolver(AppDbContext db, IOptions<BlaaizOptions> options, IHostEnvironment? environment = null)
    {
        _selectionOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.Database.GetConnectionString()).Options;
        EnvironmentName = environment?.EnvironmentName ?? Environments.Development;
        var value = options.Value;
        ConnectionKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            value.BaseUrl.Trim().TrimEnd('/') + "\n" + value.ClientId.Trim())));
    }

    public async Task<string> SelectAsync(string operationType, Guid operationId, string providerCode,
        string assetCode, string? networkCode, string purpose, bool previouslySubmitted, CancellationToken ct = default)
    {
        if (operationId == Guid.Empty || operationType is not ("Collection" or "Payout" or "CryptoDeposit" or "CryptoWithdrawal" or "CollectionAccount"))
            throw new InvalidOperationException("A valid payment operation is required for wallet selection.");
        if (purpose is not (Collection or Payout)) throw new InvalidOperationException("Invalid wallet purpose.");
        var provider = providerCode.Trim().ToUpperInvariant();
        var asset = assetCode.Trim().ToUpperInvariant();
        var network = (networkCode ?? "").Trim().ToUpperInvariant();
        // A provider selection must survive a rollback of the surrounding business
        // transaction, including a timeout after an external submission.
        await using var selectionDb = new AppDbContext(_selectionOptions);
        var selection = await FindSelectionAsync(selectionDb, operationType, operationId, ct);
        if (selection is null)
        {
            if (previouslySubmitted)
                throw new InvalidOperationException("This earlier payment has no recorded provider wallet. Reconcile its original provider request before attempting it again.");
            var query = selectionDb.ProviderWalletConfigurations.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive &&
                x.ProviderCode == provider && x.Environment == EnvironmentName && x.AssetCode == asset && x.NetworkCode == network);
            var wallet = await (purpose == Collection ? query.Where(x => x.DefaultForCollection) : query.Where(x => x.DefaultForPayout))
                .SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Configure an active default {purpose.ToLowerInvariant()} wallet for {provider}/{asset}/{network} in SuperAdmin.");
            Validate(wallet, provider, asset, network, purpose);
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            // Commit the choice before calling a provider. Concurrent requests for the same
            // operation all reload the winning selection and cannot choose different wallets.
            await selectionDb.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ProviderWalletSelections"
                    ("Id", "OperationType", "OperationId", "WalletConfigurationId", "ProviderWalletId", "Purpose", "SelectedAt")
                VALUES ({id}, {operationType}, {operationId}, {wallet.Id}, {wallet.ProviderWalletId}, {purpose}, {now})
                ON CONFLICT ("OperationType", "OperationId") DO NOTHING
                """, ct);
            selection = await FindSelectionAsync(selectionDb, operationType, operationId, ct)
                ?? throw new InvalidOperationException("Wallet selection could not be recorded.");
        }
        Validate(selection.WalletConfiguration, provider, asset, network, purpose);
        if (selection.Purpose != purpose || selection.ProviderWalletId != selection.WalletConfiguration.ProviderWalletId)
            throw new InvalidOperationException("The saved provider wallet selection does not match this payment.");
        var configuredAsset = await selectionDb.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Code == asset, ct);
        if (configuredAsset is null || !configuredAsset.IsSupported ||
            (purpose == Collection ? !configuredAsset.DepositEnabled : !configuredAsset.WithdrawalEnabled))
            throw new InvalidOperationException("This asset is not enabled for the requested wallet operation.");
        if (configuredAsset.Type == AssetType.Crypto)
        {
            var configuredNetwork = await selectionDb.AssetNetworks.AsNoTracking()
                .SingleOrDefaultAsync(x => x.AssetCode == asset && x.NetworkCode == network, ct);
            if (configuredNetwork is null || configuredNetwork.Status != AssetNetworkStatus.Active ||
                (purpose == Collection ? !configuredNetwork.DepositEnabled : !configuredNetwork.WithdrawalEnabled))
                throw new InvalidOperationException("This crypto network is not enabled for the requested wallet operation.");
        }
        return selection.ProviderWalletId;
    }

    private static Task<ProviderWalletSelection?> FindSelectionAsync(AppDbContext db, string operation, Guid id, CancellationToken ct) =>
        db.ProviderWalletSelections.AsNoTracking().Include(x => x.WalletConfiguration)
            .SingleOrDefaultAsync(x => x.OperationType == operation && x.OperationId == id, ct);

    private void Validate(ProviderWalletConfiguration wallet, string provider, string asset, string network, string purpose)
    {
        if (wallet.ProviderCode != provider || wallet.Environment != EnvironmentName || wallet.AssetCode != asset || wallet.NetworkCode != network)
            throw new InvalidOperationException("The saved wallet belongs to a different provider, environment, asset or network.");
        if (wallet.IsDeleted || !wallet.IsActive || wallet.VerifiedAt is null || wallet.VerifiedConnectionKey != ConnectionKey ||
            (purpose == Collection ? !wallet.CollectionEnabled : !wallet.PayoutEnabled))
            throw new InvalidOperationException("The selected provider wallet is suspended, unverified, or disabled for this operation. Review it in SuperAdmin; retries cannot switch wallets.");
    }
}
