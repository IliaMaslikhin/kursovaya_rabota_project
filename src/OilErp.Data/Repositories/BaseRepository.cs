using System.Data;
using Dapper;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Base repository implementation with common functionality
/// </summary>
public abstract class BaseRepository<TEntity, TKey> : IRepository<TEntity, TKey> where TEntity : class
{
    protected readonly IDbConnectionFactory _connectionFactory;

    protected BaseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets an entity by ID
    /// </summary>
    public abstract Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities
    /// </summary>
    public abstract Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new entity
    /// </summary>
    public abstract Task<TKey> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity
    /// </summary>
    public abstract Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by ID
    /// </summary>
    public abstract Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and returns results
    /// </summary>
    protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Executes a query and returns a single result
    /// </summary>
    protected async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Executes a command and returns the number of affected rows
    /// </summary>
    protected async Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, parameters);
    }

    /// <summary>
    /// Executes a command and returns a scalar value
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Executes multiple queries with multiple result sets
    /// </summary>
    protected async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryMultipleAsync(sql, parameters);
    }

    /// <summary>
    /// Executes a query against a plant database
    /// </summary>
    protected async Task<IEnumerable<T>> QueryPlantAsync<T>(string plantCode, string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreatePlantConnectionAsync(plantCode, cancellationToken);
        return await connection.QueryAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Executes a command against a plant database
    /// </summary>
    protected async Task<int> ExecutePlantAsync(string plantCode, string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreatePlantConnectionAsync(plantCode, cancellationToken);
        return await connection.ExecuteAsync(sql, parameters);
    }

    /// <summary>
    /// Builds a SQL WHERE clause from a dictionary of conditions
    /// </summary>
    protected static string BuildWhereClause(Dictionary<string, object> conditions)
    {
        if (!conditions.Any())
            return "";

        var clauses = conditions.Keys.Select(key => $"{key} = @{key}");
        return "WHERE " + string.Join(" AND ", clauses);
    }

    /// <summary>
    /// Builds a SQL ORDER BY clause
    /// </summary>
    protected static string BuildOrderByClause(string? orderBy, bool descending = false)
    {
        if (string.IsNullOrEmpty(orderBy))
            return "";

        var direction = descending ? "DESC" : "ASC";
        return $"ORDER BY {orderBy} {direction}";
    }

    /// <summary>
    /// Builds a SQL LIMIT clause
    /// </summary>
    protected static string BuildLimitClause(int? limit, int? offset = null)
    {
        if (!limit.HasValue)
            return "";

        var limitClause = $"LIMIT {limit.Value}";
        
        if (offset.HasValue && offset.Value > 0)
            limitClause += $" OFFSET {offset.Value}";

        return limitClause;
    }
}