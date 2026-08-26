using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.DigitalAssets;

public sealed class DigitalAssetEnablementService : IDigitalAssetEnablementService
{
    private readonly AppDbContext _db;
    private readonly IAuditService? _audit;

    public DigitalAssetEnablementService(
        AppDbContext db,
        IAuditService? audit = null)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DigitalAssetAdminAssetDto>> GetAssetsAsync(
        CancellationToken ct = default)
    {
        var assets = await _db.Assets
            .AsNoTracking()
            .Include(x => x.Networks)
            .Where(x => x.Type == AssetType.Crypto)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

        var codes = assets.Select(x => x.Code).ToList();

        var countryAvailability = await _db.CountryAssets
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => codes.Contains(x.AssetCode))
            .OrderBy(x => x.Country.Name)
            .ToListAsync(ct);

        return assets
            .Select(asset => ToAssetDto(
                asset,
                countryAvailability.Where(x => x.AssetCode == asset.Code)))
            .ToList();
    }

    public async Task<DigitalAssetAdminAssetDto> UpdateAssetAsync(
        string assetCode,
        Guid userId,
        UpdateDigitalAssetEnablementRequestDto request,
        CancellationToken ct = default)
    {
        var reason = RequiredReason(request.Reason);
        var normalized = Required(assetCode, 20).ToUpperInvariant();

        var asset = await _db.Assets
            .Include(x => x.Networks)
            .FirstOrDefaultAsync(x =>
                x.Code == normalized &&
                x.Type == AssetType.Crypto,
                ct)
            ?? throw new InvalidOperationException(
                $"Digital asset '{normalized}' was not found.");

        ValidateAssetRequest(asset, request);

        var oldValues = new
        {
            asset.IsSupported,
            asset.DepositEnabled,
            asset.WithdrawalEnabled,
            asset.TradingEnabled,
            asset.InstantEnabled
        };

        asset.IsSupported = request.IsSupported;
        asset.DepositEnabled = request.DepositEnabled;
        asset.WithdrawalEnabled = request.WithdrawalEnabled;
        asset.TradingEnabled = request.TradingEnabled;
        asset.InstantEnabled = request.InstantEnabled;
        asset.LastUpdatedAt = DateTime.UtcNow;

        _audit?.Stage(new AuditRecordRequest(
            Action: "DIGITAL_ASSET_ENABLEMENT_UPDATED",
            Category: "DigitalAssets",
            EntityName: nameof(Asset),
            EntityId: asset.Code,
            OldValues: oldValues,
            NewValues: new
            {
                asset.IsSupported,
                asset.DepositEnabled,
                asset.WithdrawalEnabled,
                asset.TradingEnabled,
                asset.InstantEnabled
            },
            Metadata: new
            {
                Reason = reason
            },
            UserId: userId));

        await _db.SaveChangesAsync(ct);

        return await GetAssetAsync(asset.Code, ct);
    }

    public async Task<DigitalAssetAdminNetworkDto> CreateNetworkAsync(
        string assetCode,
        Guid userId,
        CreateDigitalAssetNetworkRequestDto request,
        CancellationToken ct = default)
    {
        var reason = RequiredReason(request.Reason);
        var normalizedAsset = Required(assetCode, 20).ToUpperInvariant();
        var networkCode = Required(request.NetworkCode, 50).ToUpperInvariant();
        var name = Required(request.Name, 100);
        var nativeAssetCode = Clean(request.NativeAssetCode, 20)?.ToUpperInvariant();
        var contractAddress = Clean(request.ContractAddress, 200);

        var asset = await _db.Assets
            .SingleOrDefaultAsync(x =>
                x.Code == normalizedAsset &&
                x.Type == AssetType.Crypto,
                ct)
            ?? throw new InvalidOperationException(
                $"Digital asset '{normalizedAsset}' was not found.");

        if (!Enum.IsDefined(request.Status))
            throw new InvalidOperationException(
                "Invalid digital-asset network status.");

        if (request.RequiredConfirmations < 0 ||
            request.MinimumDeposit < 0m ||
            request.MinimumWithdrawal < 0m ||
            request.WithdrawalFee < 0m)
        {
            throw new InvalidOperationException(
                "Digital-asset network confirmations, minimums and fees cannot be negative.");
        }

        if (request.Status != AssetNetworkStatus.Active &&
            (request.DepositEnabled || request.WithdrawalEnabled))
        {
            throw new InvalidOperationException(
                "Deposit and withdrawal flags must be disabled when a digital-asset network is not active.");
        }

        if (request.Status == AssetNetworkStatus.Active &&
            !asset.IsSupported)
        {
            throw new InvalidOperationException(
                "A network cannot be activated while its digital asset is unsupported.");
        }

        if (await _db.AssetNetworks.AsNoTracking().AnyAsync(x =>
                x.AssetCode == normalizedAsset &&
                x.NetworkCode == networkCode,
                ct))
        {
            throw new InvalidOperationException(
                $"Network '{networkCode}' already exists for digital asset '{normalizedAsset}'.");
        }

        var network = new AssetNetwork
        {
            AssetCode = normalizedAsset,
            Asset = asset,
            NetworkCode = networkCode,
            Name = name,
            NativeAssetCode = nativeAssetCode,
            ContractAddress = contractAddress,
            Status = request.Status,
            RequiredConfirmations = request.RequiredConfirmations,
            MinimumDeposit = request.MinimumDeposit,
            MinimumWithdrawal = request.MinimumWithdrawal,
            WithdrawalFee = request.WithdrawalFee,
            DepositEnabled = request.DepositEnabled,
            WithdrawalEnabled = request.WithdrawalEnabled,
            CreatedAt = DateTime.UtcNow
        };

        _db.AssetNetworks.Add(network);

        _audit?.Stage(new AuditRecordRequest(
            Action: "DIGITAL_ASSET_NETWORK_CREATED",
            Category: "DigitalAssets",
            EntityName: nameof(AssetNetwork),
            EntityId: network.Id.ToString(),
            OldValues: null,
            NewValues: new
            {
                network.AssetCode,
                network.NetworkCode,
                network.Name,
                network.NativeAssetCode,
                network.ContractAddress,
                network.Status,
                network.RequiredConfirmations,
                network.MinimumDeposit,
                network.MinimumWithdrawal,
                network.WithdrawalFee,
                network.DepositEnabled,
                network.WithdrawalEnabled
            },
            Metadata: new
            {
                Reason = reason
            },
            UserId: userId));

        await _db.SaveChangesAsync(ct);

        return ToNetworkDto(network);
    }

    public async Task<DigitalAssetAdminNetworkDto> UpdateNetworkAsync(
        Guid assetNetworkId,
        Guid userId,
        UpdateDigitalAssetNetworkRequestDto request,
        CancellationToken ct = default)
    {
        var reason = RequiredReason(request.Reason);

        var network = await _db.AssetNetworks
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x =>
                x.Id == assetNetworkId &&
                x.Asset.Type == AssetType.Crypto,
                ct)
            ?? throw new InvalidOperationException(
                "Digital-asset network was not found.");

        await ValidateNetworkRequestAsync(network, request, ct);

        var oldValues = new
        {
            network.Status,
            network.RequiredConfirmations,
            network.MinimumDeposit,
            network.MinimumWithdrawal,
            network.WithdrawalFee,
            network.DepositEnabled,
            network.WithdrawalEnabled
        };

        network.Status = request.Status;
        network.RequiredConfirmations = request.RequiredConfirmations;
        network.MinimumDeposit = request.MinimumDeposit;
        network.MinimumWithdrawal = request.MinimumWithdrawal;
        network.WithdrawalFee = request.WithdrawalFee;
        network.DepositEnabled = request.DepositEnabled;
        network.WithdrawalEnabled = request.WithdrawalEnabled;
        network.LastUpdatedAt = DateTime.UtcNow;

        _audit?.Stage(new AuditRecordRequest(
            Action: "DIGITAL_ASSET_NETWORK_ENABLEMENT_UPDATED",
            Category: "DigitalAssets",
            EntityName: nameof(AssetNetwork),
            EntityId: network.Id.ToString(),
            OldValues: oldValues,
            NewValues: new
            {
                network.Status,
                network.RequiredConfirmations,
                network.MinimumDeposit,
                network.MinimumWithdrawal,
                network.WithdrawalFee,
                network.DepositEnabled,
                network.WithdrawalEnabled
            },
            Metadata: new
            {
                network.AssetCode,
                network.NetworkCode,
                Reason = reason
            },
            UserId: userId));

        await _db.SaveChangesAsync(ct);

        return ToNetworkDto(network);
    }

    public async Task<DigitalAssetCountryAvailabilityDto> UpdateCountryAvailabilityAsync(
        string assetCode,
        string countryCode,
        Guid userId,
        UpdateDigitalAssetCountryAvailabilityRequestDto request,
        CancellationToken ct = default)
    {
        var reason = RequiredReason(request.Reason);
        var normalizedAsset = Required(assetCode, 20).ToUpperInvariant();
        var normalizedCountry = Required(countryCode, 10).ToUpperInvariant();

        var assetExists = await _db.Assets
            .AsNoTracking()
            .AnyAsync(x =>
                x.Code == normalizedAsset &&
                x.Type == AssetType.Crypto,
                ct);

        if (!assetExists)
            throw new InvalidOperationException(
                $"Digital asset '{normalizedAsset}' was not found.");

        var country = await _db.Countries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedCountry, ct)
            ?? throw new InvalidOperationException(
                $"Country '{normalizedCountry}' was not found.");

        var mapping = await _db.CountryAssets
            .FirstOrDefaultAsync(x =>
                x.CountryCode == normalizedCountry &&
                x.AssetCode == normalizedAsset,
                ct);

        var oldValues = new
        {
            Exists = mapping is not null,
            CanDeposit = mapping?.CanDeposit,
            CanWithdraw = mapping?.CanWithdraw,
            CanTrade = mapping?.CanTrade,
            CanUseInstant = mapping?.CanUseInstant
        };

        if (mapping is null)
        {
            mapping = new CountryAsset
            {
                CountryCode = normalizedCountry,
                AssetCode = normalizedAsset
            };

            _db.CountryAssets.Add(mapping);
        }

        mapping.CanDeposit = request.CanDeposit;
        mapping.CanWithdraw = request.CanWithdraw;
        mapping.CanTrade = request.CanTrade;
        mapping.CanUseInstant = request.CanUseInstant;

        _audit?.Stage(new AuditRecordRequest(
            Action: "DIGITAL_ASSET_COUNTRY_AVAILABILITY_UPDATED",
            Category: "DigitalAssets",
            EntityName: nameof(CountryAsset),
            EntityId: mapping.Id.ToString(),
            OldValues: oldValues,
            NewValues: new
            {
                mapping.CountryCode,
                mapping.AssetCode,
                mapping.CanDeposit,
                mapping.CanWithdraw,
                mapping.CanTrade,
                mapping.CanUseInstant,
                mapping.CanSend,
                mapping.CanReceive,
                mapping.IsDefault
            },
            Metadata: new
            {
                CountryName = country.Name,
                Reason = reason
            },
            UserId: userId));

        await _db.SaveChangesAsync(ct);

        return new DigitalAssetCountryAvailabilityDto
        {
            CountryCode = normalizedCountry,
            CountryName = country.Name,
            CanSend = mapping.CanSend,
            CanReceive = mapping.CanReceive,
            CanDeposit = mapping.CanDeposit,
            CanWithdraw = mapping.CanWithdraw,
            CanTrade = mapping.CanTrade,
            CanUseInstant = mapping.CanUseInstant,
            IsDefault = mapping.IsDefault
        };
    }

    private async Task<DigitalAssetAdminAssetDto> GetAssetAsync(
        string assetCode,
        CancellationToken ct)
    {
        var asset = await _db.Assets
            .AsNoTracking()
            .Include(x => x.Networks)
            .SingleAsync(x =>
                x.Code == assetCode &&
                x.Type == AssetType.Crypto,
                ct);

        var countryAvailability = await _db.CountryAssets
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => x.AssetCode == asset.Code)
            .OrderBy(x => x.Country.Name)
            .ToListAsync(ct);

        return ToAssetDto(asset, countryAvailability);
    }

    private static void ValidateAssetRequest(
        Asset asset,
        UpdateDigitalAssetEnablementRequestDto request)
    {
        if (!request.IsSupported &&
            (request.DepositEnabled ||
             request.WithdrawalEnabled ||
             request.TradingEnabled ||
             request.InstantEnabled))
        {
            throw new InvalidOperationException(
                "An unsupported digital asset cannot keep deposits, withdrawals, trading or instant trading enabled.");
        }

        if (!request.IsSupported &&
            asset.Networks.Any(x =>
                x.Status != AssetNetworkStatus.Disabled ||
                x.DepositEnabled ||
                x.WithdrawalEnabled))
        {
            throw new InvalidOperationException(
                "Disable all digital-asset networks before marking the asset unsupported.");
        }

        if (request.DepositEnabled &&
            !asset.Networks.Any(x =>
                x.Status == AssetNetworkStatus.Active &&
                x.DepositEnabled))
        {
            throw new InvalidOperationException(
                "At least one active deposit-enabled network is required before enabling digital-asset deposits.");
        }

        if (request.WithdrawalEnabled &&
            !asset.Networks.Any(x =>
                x.Status == AssetNetworkStatus.Active &&
                x.WithdrawalEnabled))
        {
            throw new InvalidOperationException(
                "At least one active withdrawal-enabled network is required before enabling digital-asset withdrawals.");
        }
    }

    private async Task ValidateNetworkRequestAsync(
        AssetNetwork network,
        UpdateDigitalAssetNetworkRequestDto request,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status))
            throw new InvalidOperationException(
                "Invalid digital-asset network status.");

        if (request.RequiredConfirmations < 0 ||
            request.MinimumDeposit < 0m ||
            request.MinimumWithdrawal < 0m ||
            request.WithdrawalFee < 0m)
        {
            throw new InvalidOperationException(
                "Digital-asset network confirmations, minimums and fees cannot be negative.");
        }

        if (request.Status != AssetNetworkStatus.Active &&
            (request.DepositEnabled || request.WithdrawalEnabled))
        {
            throw new InvalidOperationException(
                "Deposit and withdrawal flags must be disabled when a digital-asset network is not active.");
        }

        if (request.Status == AssetNetworkStatus.Active &&
            !network.Asset.IsSupported)
        {
            throw new InvalidOperationException(
                "A network cannot be activated while its digital asset is unsupported.");
        }

        if (network.Asset.DepositEnabled &&
            (request.Status != AssetNetworkStatus.Active ||
             !request.DepositEnabled))
        {
            var anotherDepositNetworkExists = await _db.AssetNetworks
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != network.Id &&
                    x.AssetCode == network.AssetCode &&
                    x.Status == AssetNetworkStatus.Active &&
                    x.DepositEnabled,
                    ct);

            if (!anotherDepositNetworkExists)
            {
                throw new InvalidOperationException(
                    "Disable asset-level deposits first or enable another active deposit network before disabling this network.");
            }
        }

        if (network.Asset.WithdrawalEnabled &&
            (request.Status != AssetNetworkStatus.Active ||
             !request.WithdrawalEnabled))
        {
            var anotherWithdrawalNetworkExists = await _db.AssetNetworks
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != network.Id &&
                    x.AssetCode == network.AssetCode &&
                    x.Status == AssetNetworkStatus.Active &&
                    x.WithdrawalEnabled,
                    ct);

            if (!anotherWithdrawalNetworkExists)
            {
                throw new InvalidOperationException(
                    "Disable asset-level withdrawals first or enable another active withdrawal network before disabling this network.");
            }
        }
    }

    private static DigitalAssetAdminAssetDto ToAssetDto(
        Asset asset,
        IEnumerable<CountryAsset> countryAvailability) =>
        new()
        {
            Code = asset.Code,
            Name = asset.Name,
            Symbol = asset.Symbol,
            Type = asset.Type,
            DecimalPlaces = asset.DecimalPlaces,
            IsStablecoin = asset.IsStablecoin,
            IsSupported = asset.IsSupported,
            DepositEnabled = asset.DepositEnabled,
            WithdrawalEnabled = asset.WithdrawalEnabled,
            TradingEnabled = asset.TradingEnabled,
            InstantEnabled = asset.InstantEnabled,
            LastUpdatedAt = asset.LastUpdatedAt,
            Networks = asset.Networks
                .OrderBy(x => x.NetworkCode)
                .Select(ToNetworkDto)
                .ToList(),
            CountryAvailability = countryAvailability
                .Select(x => new DigitalAssetCountryAvailabilityDto
                {
                    CountryCode = x.CountryCode,
                    CountryName = x.Country.Name,
                    CanSend = x.CanSend,
                    CanReceive = x.CanReceive,
                    CanDeposit = x.CanDeposit,
                    CanWithdraw = x.CanWithdraw,
                    CanTrade = x.CanTrade,
                    CanUseInstant = x.CanUseInstant,
                    IsDefault = x.IsDefault
                })
                .ToList()
        };

    private static DigitalAssetAdminNetworkDto ToNetworkDto(
        AssetNetwork network) =>
        new()
        {
            Id = network.Id,
            AssetCode = network.AssetCode,
            NetworkCode = network.NetworkCode,
            Name = network.Name,
            NativeAssetCode = network.NativeAssetCode,
            ContractAddress = network.ContractAddress,
            RequiredConfirmations = network.RequiredConfirmations,
            MinimumDeposit = network.MinimumDeposit,
            MinimumWithdrawal = network.MinimumWithdrawal,
            WithdrawalFee = network.WithdrawalFee,
            DepositEnabled = network.DepositEnabled,
            WithdrawalEnabled = network.WithdrawalEnabled,
            Status = network.Status,
            LastUpdatedAt = network.LastUpdatedAt
        };

    private static string RequiredReason(string? value)
    {
        var clean = Clean(value, 1000);

        return string.IsNullOrWhiteSpace(clean)
            ? throw new InvalidOperationException(
                "A reason is required for this digital-asset administrative action.")
            : clean;
    }

    private static string Required(string? value, int max)
    {
        var clean = Clean(value, max);

        return string.IsNullOrWhiteSpace(clean)
            ? throw new InvalidOperationException("Required value is missing.")
            : clean;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var clean = value.Trim();
        return clean.Length <= max ? clean : clean[..max];
    }
}