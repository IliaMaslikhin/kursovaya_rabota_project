using System.Data;
using Dapper;
using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Repository implementation for Segment entities
/// </summary>
public class SegmentRepository : BaseRepository<Segment, Guid>, ISegmentRepository
{
    public SegmentRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<Segment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT s.id, s.asset_id as AssetId, s.segment_name as SegmentName, 
                   s.length_m as LengthM, s.material_code as MaterialCode, 
                   s.coating_code as CoatingCode, s.created_at as CreatedAt
            FROM segments s 
            WHERE s.id = @Id";

        return await QuerySingleOrDefaultAsync<Segment>(sql, new { Id = id }, cancellationToken);
    }

    public override async Task<IEnumerable<Segment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT s.id, s.asset_id as AssetId, s.segment_name as SegmentName, 
                   s.length_m as LengthM, s.material_code as MaterialCode, 
                   s.coating_code as CoatingCode, s.created_at as CreatedAt
            FROM segments s 
            ORDER BY s.created_at DESC";

        return await QueryAsync<Segment>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<Guid> CreateAsync(Segment segment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(segment);

        const string sql = @"
            INSERT INTO segments (id, asset_id, segment_name, length_m, material_code, coating_code, created_at)
            VALUES (@Id, @AssetId, @SegmentName, @LengthM, @MaterialCode, @CoatingCode, @CreatedAt)
            RETURNING id";

        if (segment.Id == Guid.Empty)
            segment.Id = Guid.NewGuid();

        segment.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            segment.Id,
            segment.AssetId,
            segment.SegmentName,
            segment.LengthM,
            segment.MaterialCode,
            segment.CoatingCode,
            segment.CreatedAt
        };

        var result = await ExecuteScalarAsync<Guid>(sql, parameters, cancellationToken);
        return result;
    }

    public override async Task UpdateAsync(Segment segment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(segment);

        const string sql = @"
            UPDATE segments 
            SET segment_name = @SegmentName,
                length_m = @LengthM,
                material_code = @MaterialCode,
                coating_code = @CoatingCode
            WHERE id = @Id";

        var parameters = new
        {
            segment.Id,
            segment.SegmentName,
            segment.LengthM,
            segment.MaterialCode,
            segment.CoatingCode
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Segment with ID '{segment.Id}' not found for update");
        }
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // First check if segment has measurement points
        const string checkSql = @"
            SELECT COUNT(*) 
            FROM measurement_points 
            WHERE segment_id = @Id";

        var measurementPointCount = await ExecuteScalarAsync<int>(checkSql, new { Id = id }, cancellationToken);
        
        if (measurementPointCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete segment with {measurementPointCount} measurement points. Delete measurement points first.");
        }

        const string sql = "DELETE FROM segments WHERE id = @Id";
        
        var affectedRows = await ExecuteAsync(sql, new { Id = id }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Segment with ID '{id}' not found for deletion");
        }
    }

    public async Task<IEnumerable<Segment>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        const string sql = @"
            SELECT s.id, s.asset_id as AssetId, s.segment_name as SegmentName, 
                   s.length_m as LengthM, s.material_code as MaterialCode, 
                   s.coating_code as CoatingCode, s.created_at as CreatedAt
            FROM segments s 
            WHERE s.asset_id = @AssetId
            ORDER BY s.segment_name";

        return await QueryAsync<Segment>(sql, new { AssetId = assetId }, cancellationToken);
    }

    public async Task<IEnumerable<Segment>> GetByMaterialCodeAsync(string materialCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialCode);

        const string sql = @"
            SELECT s.id, s.asset_id as AssetId, s.segment_name as SegmentName, 
                   s.length_m as LengthM, s.material_code as MaterialCode, 
                   s.coating_code as CoatingCode, s.created_at as CreatedAt
            FROM segments s 
            WHERE s.material_code = @MaterialCode
            ORDER BY s.created_at DESC";

        return await QueryAsync<Segment>(sql, new { MaterialCode = materialCode }, cancellationToken);
    }

    public async Task<IEnumerable<Segment>> GetByCoatingCodeAsync(string coatingCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coatingCode);

        const string sql = @"
            SELECT s.id, s.asset_id as AssetId, s.segment_name as SegmentName, 
                   s.length_m as LengthM, s.material_code as MaterialCode, 
                   s.coating_code as CoatingCode, s.created_at as CreatedAt
            FROM segments s 
            WHERE s.coating_code = @CoatingCode
            ORDER BY s.created_at DESC";

        return await QueryAsync<Segment>(sql, new { CoatingCode = coatingCode }, cancellationToken);
    }

    public async Task<Segment?> GetWithMeasurementPointsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT s.id, s.asset_id as AssetId, s.segment_name as SegmentName, 
                   s.length_m as LengthM, s.material_code as MaterialCode, 
                   s.coating_code as CoatingCode, s.created_at as CreatedAt,
                   mp.id as Id, mp.segment_id as SegmentId, mp.point_name as PointName,
                   mp.distance_from_start as DistanceFromStart, mp.measurement_type as MeasurementType,
                   mp.created_at as CreatedAt
            FROM segments s 
            LEFT JOIN measurement_points mp ON s.id = mp.segment_id
            WHERE s.id = @Id
            ORDER BY mp.distance_from_start";

        var segmentDict = new Dictionary<Guid, Segment>();

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.QueryAsync<Segment, MeasurementPoint?, Segment>(
            sql,
            (segment, measurementPoint) =>
            {
                if (!segmentDict.TryGetValue(segment.Id, out var existingSegment))
                {
                    existingSegment = segment;
                    existingSegment.MeasurementPoints = new List<MeasurementPoint>();
                    segmentDict.Add(segment.Id, existingSegment);
                }

                if (measurementPoint != null)
                {
                    existingSegment.MeasurementPoints.Add(measurementPoint);
                }

                return existingSegment;
            },
            new { Id = id },
            splitOn: "Id"
        );

        return segmentDict.Values.FirstOrDefault();
    }

    public async Task<decimal> GetTotalLengthByAssetAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        const string sql = @"
            SELECT COALESCE(SUM(length_m), 0) 
            FROM segments 
            WHERE asset_id = @AssetId";

        var result = await ExecuteScalarAsync<decimal?>(sql, new { AssetId = assetId }, cancellationToken);
        return result ?? 0m;
    }
}