using System.Data;
using Dapper;
using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Repository implementation for MeasurementPoint entities
/// </summary>
public class MeasurementPointRepository : BaseRepository<MeasurementPoint, Guid>, IMeasurementPointRepository
{
    public MeasurementPointRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<MeasurementPoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT mp.id, mp.segment_id as SegmentId, mp.point_name as PointName,
                   mp.distance_from_start as DistanceFromStart, mp.measurement_type as MeasurementType,
                   mp.created_at as CreatedAt
            FROM measurement_points mp 
            WHERE mp.id = @Id";

        return await QuerySingleOrDefaultAsync<MeasurementPoint>(sql, new { Id = id }, cancellationToken);
    }

    public override async Task<IEnumerable<MeasurementPoint>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT mp.id, mp.segment_id as SegmentId, mp.point_name as PointName,
                   mp.distance_from_start as DistanceFromStart, mp.measurement_type as MeasurementType,
                   mp.created_at as CreatedAt
            FROM measurement_points mp 
            ORDER BY mp.created_at DESC";

        return await QueryAsync<MeasurementPoint>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<Guid> CreateAsync(MeasurementPoint measurementPoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(measurementPoint);

        // Validate that segment exists
        const string validateSql = @"
            SELECT COUNT(*) 
            FROM segments 
            WHERE id = @SegmentId";

        var segmentExists = await ExecuteScalarAsync<int>(validateSql, new { measurementPoint.SegmentId }, cancellationToken) > 0;
        
        if (!segmentExists)
        {
            throw new InvalidOperationException($"Segment with ID '{measurementPoint.SegmentId}' does not exist");
        }

        // Check if distance from start is within segment length
        const string segmentLengthSql = @"
            SELECT length_m 
            FROM segments 
            WHERE id = @SegmentId";

        var segmentLength = await ExecuteScalarAsync<decimal>(segmentLengthSql, new { measurementPoint.SegmentId }, cancellationToken);
        
        if (measurementPoint.DistanceFromStart > segmentLength)
        {
            throw new InvalidOperationException($"Measurement point distance ({measurementPoint.DistanceFromStart}m) cannot exceed segment length ({segmentLength}m)");
        }

        const string sql = @"
            INSERT INTO measurement_points (id, segment_id, point_name, distance_from_start, measurement_type, created_at)
            VALUES (@Id, @SegmentId, @PointName, @DistanceFromStart, @MeasurementType, @CreatedAt)
            RETURNING id";

        if (measurementPoint.Id == Guid.Empty)
            measurementPoint.Id = Guid.NewGuid();

        measurementPoint.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            measurementPoint.Id,
            measurementPoint.SegmentId,
            measurementPoint.PointName,
            measurementPoint.DistanceFromStart,
            measurementPoint.MeasurementType,
            measurementPoint.CreatedAt
        };

        var result = await ExecuteScalarAsync<Guid>(sql, parameters, cancellationToken);
        return result;
    }

    public override async Task UpdateAsync(MeasurementPoint measurementPoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(measurementPoint);

        // Check if distance from start is within segment length
        const string segmentLengthSql = @"
            SELECT length_m 
            FROM segments 
            WHERE id = @SegmentId";

        var segmentLength = await ExecuteScalarAsync<decimal>(segmentLengthSql, new { measurementPoint.SegmentId }, cancellationToken);
        
        if (measurementPoint.DistanceFromStart > segmentLength)
        {
            throw new InvalidOperationException($"Measurement point distance ({measurementPoint.DistanceFromStart}m) cannot exceed segment length ({segmentLength}m)");
        }

        const string sql = @"
            UPDATE measurement_points 
            SET point_name = @PointName,
                distance_from_start = @DistanceFromStart,
                measurement_type = @MeasurementType
            WHERE id = @Id";

        var parameters = new
        {
            measurementPoint.Id,
            measurementPoint.PointName,
            measurementPoint.DistanceFromStart,
            measurementPoint.MeasurementType
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Measurement point with ID '{measurementPoint.Id}' not found for update");
        }
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // First check if measurement point has readings
        const string checkSql = @"
            SELECT COUNT(*) 
            FROM readings 
            WHERE point_id = @Id";

        var readingCount = await ExecuteScalarAsync<int>(checkSql, new { Id = id }, cancellationToken);
        
        if (readingCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete measurement point with {readingCount} readings. Delete readings first.");
        }

        const string sql = "DELETE FROM measurement_points WHERE id = @Id";
        
        var affectedRows = await ExecuteAsync(sql, new { Id = id }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Measurement point with ID '{id}' not found for deletion");
        }
    }

    public async Task<IEnumerable<MeasurementPoint>> GetBySegmentIdAsync(Guid segmentId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT mp.id, mp.segment_id as SegmentId, mp.point_name as PointName,
                   mp.distance_from_start as DistanceFromStart, mp.measurement_type as MeasurementType,
                   mp.created_at as CreatedAt
            FROM measurement_points mp 
            WHERE mp.segment_id = @SegmentId
            ORDER BY mp.distance_from_start";

        return await QueryAsync<MeasurementPoint>(sql, new { SegmentId = segmentId }, cancellationToken);
    }

    public async Task<IEnumerable<MeasurementPoint>> GetByMeasurementTypeAsync(string measurementType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(measurementType);

        const string sql = @"
            SELECT mp.id, mp.segment_id as SegmentId, mp.point_name as PointName,
                   mp.distance_from_start as DistanceFromStart, mp.measurement_type as MeasurementType,
                   mp.created_at as CreatedAt
            FROM measurement_points mp 
            WHERE mp.measurement_type = @MeasurementType
            ORDER BY mp.created_at DESC";

        return await QueryAsync<MeasurementPoint>(sql, new { MeasurementType = measurementType }, cancellationToken);
    }

    public async Task<MeasurementPoint?> GetWithReadingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT mp.id, mp.segment_id as SegmentId, mp.point_name as PointName,
                   mp.distance_from_start as DistanceFromStart, mp.measurement_type as MeasurementType,
                   mp.created_at as CreatedAt,
                   r.id as Id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId, r.is_valid as IsValid,
                   r.created_at as CreatedAt
            FROM measurement_points mp 
            LEFT JOIN readings r ON mp.id = r.point_id
            WHERE mp.id = @Id
            ORDER BY r.measured_at DESC";

        var measurementPointDict = new Dictionary<Guid, MeasurementPoint>();

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.QueryAsync<MeasurementPoint, Reading?, MeasurementPoint>(
            sql,
            (measurementPoint, reading) =>
            {
                if (!measurementPointDict.TryGetValue(measurementPoint.Id, out var existingMeasurementPoint))
                {
                    existingMeasurementPoint = measurementPoint;
                    existingMeasurementPoint.Readings = new List<Reading>();
                    measurementPointDict.Add(measurementPoint.Id, existingMeasurementPoint);
                }

                if (reading != null)
                {
                    existingMeasurementPoint.Readings.Add(reading);
                }

                return existingMeasurementPoint;
            },
            new { Id = id },
            splitOn: "Id"
        );

        return measurementPointDict.Values.FirstOrDefault();
    }

    public async Task<IEnumerable<MeasurementPoint>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        const string sql = @"
            SELECT mp.id, mp.segment_id as SegmentId, mp.point_name as PointName,
                   mp.distance_from_start as DistanceFromStart, mp.measurement_type as MeasurementType,
                   mp.created_at as CreatedAt
            FROM measurement_points mp 
            INNER JOIN segments s ON mp.segment_id = s.id
            WHERE s.asset_id = @AssetId
            ORDER BY s.segment_name, mp.distance_from_start";

        return await QueryAsync<MeasurementPoint>(sql, new { AssetId = assetId }, cancellationToken);
    }
}