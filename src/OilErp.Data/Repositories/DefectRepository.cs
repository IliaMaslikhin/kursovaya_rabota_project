using System.Data;
using Dapper;
using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Repository implementation for Defect entities with severity-based queries
/// </summary>
public class DefectRepository : BaseRepository<Defect, Guid>, IDefectRepository
{
    public DefectRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<Defect?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.id = @Id";

        return await QuerySingleOrDefaultAsync<Defect>(sql, new { Id = id }, cancellationToken);
    }

    public override async Task<IEnumerable<Defect>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            ORDER BY d.discovered_at DESC";

        return await QueryAsync<Defect>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<Guid> CreateAsync(Defect defect, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(defect);

        // Validate that asset exists
        const string validateSql = @"
            SELECT COUNT(*) 
            FROM assets.global_assets 
            WHERE id = @AssetId";

        var assetExists = await ExecuteScalarAsync<int>(validateSql, new { defect.AssetId }, cancellationToken) > 0;
        
        if (!assetExists)
        {
            throw new InvalidOperationException($"Asset with ID '{defect.AssetId}' does not exist");
        }

        const string sql = @"
            INSERT INTO defects (id, asset_id, defect_type, severity, description, discovered_at, 
                               created_at, discovered_by, location, is_resolved, resolved_at, resolution)
            VALUES (@Id, @AssetId, @DefectType, @Severity, @Description, @DiscoveredAt, 
                   @CreatedAt, @DiscoveredBy, @Location, @IsResolved, @ResolvedAt, @Resolution)
            RETURNING id";

        if (defect.Id == Guid.Empty)
            defect.Id = Guid.NewGuid();

        defect.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            defect.Id,
            defect.AssetId,
            defect.DefectType,
            defect.Severity,
            defect.Description,
            defect.DiscoveredAt,
            defect.CreatedAt,
            defect.DiscoveredBy,
            defect.Location,
            defect.IsResolved,
            defect.ResolvedAt,
            defect.Resolution
        };

        var result = await ExecuteScalarAsync<Guid>(sql, parameters, cancellationToken);
        return result;
    }

    public override async Task UpdateAsync(Defect defect, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(defect);

        const string sql = @"
            UPDATE defects 
            SET defect_type = @DefectType,
                severity = @Severity,
                description = @Description,
                discovered_at = @DiscoveredAt,
                discovered_by = @DiscoveredBy,
                location = @Location,
                is_resolved = @IsResolved,
                resolved_at = @ResolvedAt,
                resolution = @Resolution
            WHERE id = @Id";

        var parameters = new
        {
            defect.Id,
            defect.DefectType,
            defect.Severity,
            defect.Description,
            defect.DiscoveredAt,
            defect.DiscoveredBy,
            defect.Location,
            defect.IsResolved,
            defect.ResolvedAt,
            defect.Resolution
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Defect with ID '{defect.Id}' not found for update");
        }
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // First check if defect has work orders
        const string checkSql = @"
            SELECT COUNT(*) 
            FROM work_orders 
            WHERE defect_id = @Id";

        var workOrderCount = await ExecuteScalarAsync<int>(checkSql, new { Id = id }, cancellationToken);
        
        if (workOrderCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete defect with {workOrderCount} work orders. Delete work orders first or resolve the defect.");
        }

        const string sql = "DELETE FROM defects WHERE id = @Id";
        
        var affectedRows = await ExecuteAsync(sql, new { Id = id }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Defect with ID '{id}' not found for deletion");
        }
    }

    public async Task<IEnumerable<Defect>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.asset_id = @AssetId
            ORDER BY d.discovered_at DESC";

        return await QueryAsync<Defect>(sql, new { AssetId = assetId }, cancellationToken);
    }

    public async Task<IEnumerable<Defect>> GetBySeverityAsync(string severity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);

        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.severity = @Severity
            ORDER BY d.discovered_at DESC";

        return await QueryAsync<Defect>(sql, new { Severity = severity }, cancellationToken);
    }

    public async Task<IEnumerable<Defect>> GetByDefectTypeAsync(string defectType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defectType);

        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.defect_type = @DefectType
            ORDER BY d.discovered_at DESC";

        return await QueryAsync<Defect>(sql, new { DefectType = defectType }, cancellationToken);
    }

    public async Task<IEnumerable<Defect>> GetUnresolvedAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.is_resolved = false
            ORDER BY 
                CASE d.severity 
                    WHEN 'Critical' THEN 1
                    WHEN 'High' THEN 2
                    WHEN 'Medium' THEN 3
                    WHEN 'Low' THEN 4
                    ELSE 5
                END,
                d.discovered_at ASC";

        return await QueryAsync<Defect>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<Defect>> GetResolvedAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.is_resolved = true
            ORDER BY d.resolved_at DESC";

        return await QueryAsync<Defect>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<Defect>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.discovered_at >= @FromDate AND d.discovered_at <= @ToDate
            ORDER BY d.discovered_at DESC";

        return await QueryAsync<Defect>(sql, new { FromDate = fromDate, ToDate = toDate }, cancellationToken);
    }

    public async Task<IEnumerable<Defect>> GetCriticalDefectsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT d.id, d.asset_id as AssetId, d.defect_type as DefectType,
                   d.severity as Severity, d.description as Description,
                   d.discovered_at as DiscoveredAt, d.created_at as CreatedAt,
                   d.discovered_by as DiscoveredBy, d.location as Location,
                   d.is_resolved as IsResolved, d.resolved_at as ResolvedAt,
                   d.resolution as Resolution
            FROM defects d 
            WHERE d.severity = 'Critical' AND d.is_resolved = false
            ORDER BY d.discovered_at ASC";

        return await QueryAsync<Defect>(sql, cancellationToken: cancellationToken);
    }

    public async Task<int> GetCountBySeverityAsync(string severity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);

        const string sql = @"
            SELECT COUNT(*) 
            FROM defects 
            WHERE severity = @Severity";

        return await ExecuteScalarAsync<int>(sql, new { Severity = severity }, cancellationToken);
    }

    public async Task<int> GetUnresolvedCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM defects 
            WHERE is_resolved = false";

        return await ExecuteScalarAsync<int>(sql, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets defects grouped by severity with counts
    /// </summary>
    public async Task<Dictionary<string, int>> GetDefectCountsBySeverityAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT severity, COUNT(*) as count
            FROM defects 
            WHERE is_resolved = false
            GROUP BY severity";

        var results = await QueryAsync<(string severity, int count)>(sql, cancellationToken: cancellationToken);
        return results.ToDictionary(r => r.severity, r => r.count);
    }

    /// <summary>
    /// Gets defect trends over time
    /// </summary>
    public async Task<IEnumerable<(DateTime Date, int NewDefects, int ResolvedDefects)>> GetDefectTrendsAsync(
        DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                DATE_TRUNC('day', d.discovered_at) as date,
                COUNT(*) as new_defects,
                COUNT(CASE WHEN d.resolved_at IS NOT NULL AND DATE_TRUNC('day', d.resolved_at) = DATE_TRUNC('day', d.discovered_at) THEN 1 END) as resolved_defects
            FROM defects d
            WHERE d.discovered_at >= @FromDate AND d.discovered_at <= @ToDate
            GROUP BY DATE_TRUNC('day', d.discovered_at)
            ORDER BY date";

        var results = await QueryAsync<dynamic>(sql, new { FromDate = fromDate, ToDate = toDate }, cancellationToken);
        
        return results.Select(r => (
            Date: (DateTime)r.date,
            NewDefects: (int)r.new_defects,
            ResolvedDefects: (int)r.resolved_defects
        ));
    }
}