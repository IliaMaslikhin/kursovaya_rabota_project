using System.Data;
using Dapper;
using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Repository implementation for Reading entities with time-series data handling
/// </summary>
public class ReadingRepository : BaseRepository<Reading, Guid>, IReadingRepository
{
    public ReadingRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<Reading?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            WHERE r.id = @Id";

        return await QuerySingleOrDefaultAsync<Reading>(sql, new { Id = id }, cancellationToken);
    }

    public override async Task<IEnumerable<Reading>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            ORDER BY r.measured_at DESC
            LIMIT 1000"; // Limit for performance

        return await QueryAsync<Reading>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<Guid> CreateAsync(Reading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        // Validate that measurement point exists
        const string validateSql = @"
            SELECT COUNT(*) 
            FROM measurement_points 
            WHERE id = @PointId";

        var pointExists = await ExecuteScalarAsync<int>(validateSql, new { reading.PointId }, cancellationToken) > 0;
        
        if (!pointExists)
        {
            throw new InvalidOperationException($"Measurement point with ID '{reading.PointId}' does not exist");
        }

        const string sql = @"
            INSERT INTO readings (id, point_id, value, unit, measured_at, operator_id, created_at, notes, is_valid)
            VALUES (@Id, @PointId, @Value, @Unit, @MeasuredAt, @OperatorId, @CreatedAt, @Notes, @IsValid)
            RETURNING id";

        if (reading.Id == Guid.Empty)
            reading.Id = Guid.NewGuid();

        reading.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            reading.Id,
            reading.PointId,
            reading.Value,
            reading.Unit,
            reading.MeasuredAt,
            reading.OperatorId,
            reading.CreatedAt,
            reading.Notes,
            reading.IsValid
        };

        var result = await ExecuteScalarAsync<Guid>(sql, parameters, cancellationToken);
        return result;
    }

    public override async Task UpdateAsync(Reading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        const string sql = @"
            UPDATE readings 
            SET value = @Value,
                unit = @Unit,
                measured_at = @MeasuredAt,
                operator_id = @OperatorId,
                notes = @Notes,
                is_valid = @IsValid
            WHERE id = @Id";

        var parameters = new
        {
            reading.Id,
            reading.Value,
            reading.Unit,
            reading.MeasuredAt,
            reading.OperatorId,
            reading.Notes,
            reading.IsValid
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Reading with ID '{reading.Id}' not found for update");
        }
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM readings WHERE id = @Id";
        
        var affectedRows = await ExecuteAsync(sql, new { Id = id }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Reading with ID '{id}' not found for deletion");
        }
    }

    public async Task<IEnumerable<Reading>> GetByPointIdAsync(Guid pointId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            WHERE r.point_id = @PointId
            ORDER BY r.measured_at DESC";

        return await QueryAsync<Reading>(sql, new { PointId = pointId }, cancellationToken);
    }

    public async Task<IEnumerable<Reading>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            WHERE r.measured_at >= @FromDate AND r.measured_at <= @ToDate
            ORDER BY r.measured_at DESC";

        return await QueryAsync<Reading>(sql, new { FromDate = fromDate, ToDate = toDate }, cancellationToken);
    }

    public async Task<IEnumerable<Reading>> GetByOperatorAsync(string operatorId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);

        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            WHERE r.operator_id = @OperatorId
            ORDER BY r.measured_at DESC";

        return await QueryAsync<Reading>(sql, new { OperatorId = operatorId }, cancellationToken);
    }

    public async Task<Reading?> GetLatestByPointIdAsync(Guid pointId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            WHERE r.point_id = @PointId
            ORDER BY r.measured_at DESC
            LIMIT 1";

        return await QuerySingleOrDefaultAsync<Reading>(sql, new { PointId = pointId }, cancellationToken);
    }

    public async Task<IEnumerable<Reading>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            INNER JOIN measurement_points mp ON r.point_id = mp.id
            INNER JOIN segments s ON mp.segment_id = s.id
            WHERE s.asset_id = @AssetId
            ORDER BY r.measured_at DESC";

        return await QueryAsync<Reading>(sql, new { AssetId = assetId }, cancellationToken);
    }

    public async Task<IEnumerable<Reading>> GetInvalidReadingsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT r.id, r.point_id as PointId, r.value as Value, r.unit as Unit,
                   r.measured_at as MeasuredAt, r.operator_id as OperatorId,
                   r.created_at as CreatedAt, r.notes as Notes, r.is_valid as IsValid
            FROM readings r 
            WHERE r.is_valid = false
            ORDER BY r.measured_at DESC";

        return await QueryAsync<Reading>(sql, cancellationToken: cancellationToken);
    }

    public async Task<decimal?> GetAverageByPointIdAsync(Guid pointId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT AVG(value) 
            FROM readings 
            WHERE point_id = @PointId AND is_valid = true";

        var parameters = new Dictionary<string, object> { { "PointId", pointId } };

        if (fromDate.HasValue)
        {
            sql += " AND measured_at >= @FromDate";
            parameters.Add("FromDate", fromDate.Value);
        }

        if (toDate.HasValue)
        {
            sql += " AND measured_at <= @ToDate";
            parameters.Add("ToDate", toDate.Value);
        }

        return await ExecuteScalarAsync<decimal?>(sql, parameters, cancellationToken);
    }

    public async Task<decimal?> GetMinValueByPointIdAsync(Guid pointId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT MIN(value) 
            FROM readings 
            WHERE point_id = @PointId AND is_valid = true";

        var parameters = new Dictionary<string, object> { { "PointId", pointId } };

        if (fromDate.HasValue)
        {
            sql += " AND measured_at >= @FromDate";
            parameters.Add("FromDate", fromDate.Value);
        }

        if (toDate.HasValue)
        {
            sql += " AND measured_at <= @ToDate";
            parameters.Add("ToDate", toDate.Value);
        }

        return await ExecuteScalarAsync<decimal?>(sql, parameters, cancellationToken);
    }

    public async Task<decimal?> GetMaxValueByPointIdAsync(Guid pointId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT MAX(value) 
            FROM readings 
            WHERE point_id = @PointId AND is_valid = true";

        var parameters = new Dictionary<string, object> { { "PointId", pointId } };

        if (fromDate.HasValue)
        {
            sql += " AND measured_at >= @FromDate";
            parameters.Add("FromDate", fromDate.Value);
        }

        if (toDate.HasValue)
        {
            sql += " AND measured_at <= @ToDate";
            parameters.Add("ToDate", toDate.Value);
        }

        return await ExecuteScalarAsync<decimal?>(sql, parameters, cancellationToken);
    }

    /// <summary>
    /// Bulk insert readings for performance optimization
    /// </summary>
    public async Task<int> BulkCreateAsync(IEnumerable<Reading> readings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var readingList = readings.ToList();
        if (!readingList.Any())
            return 0;

        // Set IDs and timestamps
        foreach (var reading in readingList)
        {
            if (reading.Id == Guid.Empty)
                reading.Id = Guid.NewGuid();
            reading.CreatedAt = DateTime.UtcNow;
        }

        const string sql = @"
            INSERT INTO readings (id, point_id, value, unit, measured_at, operator_id, created_at, notes, is_valid)
            VALUES (@Id, @PointId, @Value, @Unit, @MeasuredAt, @OperatorId, @CreatedAt, @Notes, @IsValid)";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, readingList);
    }
}