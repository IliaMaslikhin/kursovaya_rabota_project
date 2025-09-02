using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Infrastructure;

/// <summary>
/// Factory for creating database connections to central and plant databases
/// </summary>
public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Creates a connection to the central database
    /// </summary>
    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionString();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// Creates a connection to a specific plant database
    /// </summary>
    public async Task<IDbConnection> CreatePlantConnectionAsync(string plantCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plantCode);
        
        var connectionString = GetPlantConnectionString(plantCode);
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// Gets the central database connection string
    /// </summary>
    public string GetConnectionString()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection string not found in configuration");
        }
        return connectionString;
    }

    /// <summary>
    /// Gets the connection string for a specific plant database
    /// </summary>
    public string GetPlantConnectionString(string plantCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plantCode);
        
        // Try to get plant-specific connection string
        var plantConnectionString = _configuration.GetConnectionString($"Plant_{plantCode}");
        
        if (!string.IsNullOrEmpty(plantConnectionString))
        {
            return plantConnectionString;
        }

        // Fallback to default connection with plant-specific database name
        var defaultConnectionString = GetConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(defaultConnectionString);
        
        // Modify database name to include plant code
        var originalDatabase = builder.Database ?? "oilerp_central";
        builder.Database = $"oilerp_plant_{plantCode.ToLower()}";
        
        return builder.ConnectionString;
    }
}

/// <summary>
/// Manages database schema operations and migrations
/// </summary>
public class DbSchemaManager : IDbSchemaManager
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;

    public DbSchemaManager(IDbConnectionFactory connectionFactory, IConfiguration configuration)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Initializes the central database schema
    /// </summary>
    public async Task InitializeCentralDatabaseAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        
        // Read the central schema SQL file
        var schemaScript = await ReadSchemaFileAsync("central", "0001_initial_schema.sql");
        
        using var command = connection.CreateCommand();
        command.CommandText = schemaScript;
        command.CommandTimeout = 300; // 5 minutes for schema creation
        
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Initializes a plant database schema
    /// </summary>
    public async Task InitializePlantDatabaseAsync(string plantCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plantCode);
        
        using var connection = await _connectionFactory.CreatePlantConnectionAsync(plantCode, cancellationToken);
        
        // Read the plant schema SQL file
        var schemaScript = await ReadSchemaFileAsync("plants", "0001_plant_schema.sql");
        
        using var command = connection.CreateCommand();
        command.CommandText = schemaScript;
        command.CommandTimeout = 300; // 5 minutes for schema creation
        
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Checks if the central database is initialized
    /// </summary>
    public async Task<bool> IsCentralDatabaseInitializedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.schemata 
                    WHERE schema_name = 'assets'
                )";
            
            var result = command.ExecuteScalar();
            return Convert.ToBoolean(result);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a plant database is initialized
    /// </summary>
    public async Task<bool> IsPlantDatabaseInitializedAsync(string plantCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plantCode);
        
        try
        {
            using var connection = await _connectionFactory.CreatePlantConnectionAsync(plantCode, cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.schemata 
                    WHERE schema_name = 'local_assets'
                )";
            
            var result = command.ExecuteScalar();
            return Convert.ToBoolean(result);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs database migrations
    /// </summary>
    public async Task RunMigrationsAsync(CancellationToken cancellationToken = default)
    {
        // Initialize central database if not already done
        if (!await IsCentralDatabaseInitializedAsync(cancellationToken))
        {
            await InitializeCentralDatabaseAsync(cancellationToken);
        }

        // Additional migration logic would go here
        // For now, we just ensure the initial schema is in place
    }

    /// <summary>
    /// Reads a schema file from the sql directory
    /// </summary>
    private async Task<string> ReadSchemaFileAsync(string subdirectory, string filename)
    {
        // Get the solution root directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionRoot = FindSolutionRoot(currentDirectory);
        
        if (solutionRoot == null)
        {
            throw new InvalidOperationException("Could not find solution root directory");
        }

        var filePath = Path.Combine(solutionRoot, "sql", subdirectory, filename);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Schema file not found: {filePath}");
        }

        return await File.ReadAllTextAsync(filePath);
    }

    /// <summary>
    /// Finds the solution root directory by looking for the .sln file
    /// </summary>
    private static string? FindSolutionRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        
        while (directory != null)
        {
            if (directory.GetFiles("*.sln").Any())
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        
        return null;
    }
}