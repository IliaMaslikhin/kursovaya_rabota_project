namespace OilErp.Infrastructure.Config;

/// <summary>
/// Storage configuration settings
/// </summary>
/// <param name="ConnectionString">Database connection string</param>
/// <param name="CommandTimeoutSeconds">Command timeout in seconds</param>
/// <param name="DisableRoutineMetadataCache">Disable pg_proc cache/inspection cache (diagnostics)</param>
public record StorageConfig(
    string ConnectionString,
    int CommandTimeoutSeconds = 30,
    bool DisableRoutineMetadataCache = false
);
