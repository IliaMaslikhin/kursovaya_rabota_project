using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;
using OilErp.Domain.ValueObjects;

namespace OilErp.Domain.Services;

/// <summary>
/// Domain service for asset management operations
/// </summary>
public class AssetService
{
    private readonly IUnitOfWork _unitOfWork;

    public AssetService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Creates a new asset with validation
    /// </summary>
    public async Task<string> CreateAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        // Validate unique tag number
        var existingAsset = await _unitOfWork.Assets.GetByTagNumberAsync(asset.TagNumber, cancellationToken);
        if (existingAsset != null)
        {
            throw new InvalidOperationException($"Asset with tag number '{asset.TagNumber}' already exists");
        }

        // Validate plant code exists (this would be enhanced with plant validation)
        if (string.IsNullOrWhiteSpace(asset.PlantCode))
        {
            throw new ArgumentException("Plant code is required");
        }

        asset.CreatedAt = DateTime.UtcNow;
        asset.UpdatedAt = DateTime.UtcNow;

        return await _unitOfWork.Assets.CreateAsync(asset, cancellationToken);
    }

    /// <summary>
    /// Updates an existing asset
    /// </summary>
    public async Task UpdateAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var existingAsset = await _unitOfWork.Assets.GetByIdAsync(asset.Id, cancellationToken);
        if (existingAsset == null)
        {
            throw new InvalidOperationException($"Asset with ID '{asset.Id}' not found");
        }

        // Check if tag number is being changed and ensure uniqueness
        if (asset.TagNumber != existingAsset.TagNumber)
        {
            var assetWithSameTag = await _unitOfWork.Assets.GetByTagNumberAsync(asset.TagNumber, cancellationToken);
            if (assetWithSameTag != null && assetWithSameTag.Id != asset.Id)
            {
                throw new InvalidOperationException($"Asset with tag number '{asset.TagNumber}' already exists");
            }
        }

        asset.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Assets.UpdateAsync(asset, cancellationToken);
    }

    /// <summary>
    /// Adds a segment to an asset with validation
    /// </summary>
    public async Task<Guid> AddSegmentToAssetAsync(string assetId, Segment segment, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentNullException.ThrowIfNull(segment);

        var asset = await _unitOfWork.Assets.GetByIdAsync(assetId, cancellationToken);
        if (asset == null)
        {
            throw new InvalidOperationException($"Asset with ID '{assetId}' not found");
        }

        segment.AssetId = assetId;
        segment.CreatedAt = DateTime.UtcNow;

        // Validate material and coating codes exist (enhanced with catalog validation)
        if (!string.IsNullOrEmpty(segment.MaterialCode))
        {
            var material = await _unitOfWork.Materials.GetByCodeAsync(segment.MaterialCode, cancellationToken);
            if (material == null)
            {
                throw new InvalidOperationException($"Material with code '{segment.MaterialCode}' not found");
            }
        }

        if (!string.IsNullOrEmpty(segment.CoatingCode))
        {
            var coating = await _unitOfWork.Coatings.GetByCodeAsync(segment.CoatingCode, cancellationToken);
            if (coating == null)
            {
                throw new InvalidOperationException($"Coating with code '{segment.CoatingCode}' not found");
            }
        }

        return await _unitOfWork.Segments.CreateAsync(segment, cancellationToken);
    }

    /// <summary>
    /// Gets asset health status based on defects and measurements
    /// </summary>
    public async Task<string> GetAssetHealthStatusAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var asset = await _unitOfWork.Assets.GetWithDefectsAsync(assetId, cancellationToken);
        if (asset == null)
        {
            throw new InvalidOperationException($"Asset with ID '{assetId}' not found");
        }

        // Check for critical defects
        if (asset.Defects.Any(d => d.IsCritical() && !d.IsResolved))
        {
            return "Critical";
        }

        // Check for high severity defects
        if (asset.Defects.Any(d => d.IsHigh() && !d.IsResolved))
        {
            return "Poor";
        }

        // Check for medium severity defects
        if (asset.Defects.Any(d => d.IsMedium() && !d.IsResolved))
        {
            return "Fair";
        }

        // Check for any unresolved defects
        if (asset.Defects.Any(d => !d.IsResolved))
        {
            return "Good";
        }

        return "Excellent";
    }

    /// <summary>
    /// Gets assets requiring immediate attention
    /// </summary>
    public async Task<IEnumerable<Asset>> GetAssetsRequiringAttentionAsync(CancellationToken cancellationToken = default)
    {
        var criticalAssets = await _unitOfWork.Assets.GetAssetsWithCriticalDefectsAsync(cancellationToken);
        var overdueAssets = await _unitOfWork.Assets.GetAssetsWithOverdueWorkOrdersAsync(cancellationToken);

        return criticalAssets.Union(overdueAssets).Distinct();
    }

    /// <summary>
    /// Calculates asset utilization metrics
    /// </summary>
    public async Task<AssetUtilizationMetrics> CalculateUtilizationMetricsAsync(string assetId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var workOrders = await _unitOfWork.WorkOrders.GetByAssetIdAsync(assetId, cancellationToken);
        var defects = await _unitOfWork.Defects.GetByAssetIdAsync(assetId, cancellationToken);

        var maintenanceWorkOrders = workOrders.Where(w => 
            w.CompletedAt.HasValue && 
            w.CompletedAt >= fromDate && 
            w.CompletedAt <= toDate).ToList();

        var totalMaintenanceHours = maintenanceWorkOrders.Sum(w => w.ActualHours ?? 0);
        var totalPeriodHours = (decimal)(toDate - fromDate).TotalHours;
        var downtime = totalMaintenanceHours;
        var uptime = totalPeriodHours - downtime;

        var newDefects = defects.Where(d => d.DiscoveredAt >= fromDate && d.DiscoveredAt <= toDate).Count();
        var resolvedDefects = defects.Where(d => d.ResolvedAt.HasValue && d.ResolvedAt >= fromDate && d.ResolvedAt <= toDate).Count();

        return new AssetUtilizationMetrics
        {
            AssetId = assetId,
            Period = $"{fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}",
            UptimePercentage = totalPeriodHours > 0 ? (uptime / totalPeriodHours) * 100 : 0,
            DowntimeHours = downtime,
            MaintenanceWorkOrders = maintenanceWorkOrders.Count,
            NewDefects = newDefects,
            ResolvedDefects = resolvedDefects,
            CalculatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Data transfer object for asset utilization metrics
/// </summary>
public record AssetUtilizationMetrics
{
    public required string AssetId { get; init; }
    public required string Period { get; init; }
    public decimal UptimePercentage { get; init; }
    public decimal DowntimeHours { get; init; }
    public int MaintenanceWorkOrders { get; init; }
    public int NewDefects { get; init; }
    public int ResolvedDefects { get; init; }
    public DateTime CalculatedAt { get; init; }
}