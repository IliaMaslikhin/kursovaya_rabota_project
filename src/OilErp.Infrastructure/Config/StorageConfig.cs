namespace OilErp.Infrastructure.Config;

/// <summary>
/// Storage configuration settings
/// </summary>
/// <param name="ConnectionString">Database connection string</param>
/// <param name="CommandTimeoutSeconds">Command timeout in seconds</param>
public record StorageConfig(
    string ConnectionString,
    int CommandTimeoutSeconds = 30
);
