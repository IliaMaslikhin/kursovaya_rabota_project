using System.Data;
using OilErp.Domain.Entities;

namespace OilErp.Domain.Interfaces;

/// <summary>
/// Repository interface for Material catalog entities
/// </summary>
public interface IMaterialRepository
{
    Task<Material?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<Material>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<string> CreateAsync(Material material, CancellationToken cancellationToken = default);
    Task UpdateAsync(Material material, CancellationToken cancellationToken = default);
    Task DeleteAsync(string code, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<Material>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Material>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Coating catalog entities
/// </summary>
public interface ICoatingRepository
{
    Task<Coating?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<Coating>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<string> CreateAsync(Coating coating, CancellationToken cancellationToken = default);
    Task UpdateAsync(Coating coating, CancellationToken cancellationToken = default);
    Task DeleteAsync(string code, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<Coating>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Coating>> GetByManufacturerAsync(string manufacturer, CancellationToken cancellationToken = default);
    Task<IEnumerable<Coating>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Fluid catalog entities
/// </summary>
public interface IFluidRepository
{
    Task<Fluid?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<Fluid>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<string> CreateAsync(Fluid fluid, CancellationToken cancellationToken = default);
    Task UpdateAsync(Fluid fluid, CancellationToken cancellationToken = default);
    Task DeleteAsync(string code, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<Fluid>> GetByCorrosivityAsync(string corrosivity, CancellationToken cancellationToken = default);
    Task<IEnumerable<Fluid>> GetCorrosiveFluidsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Fluid>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of Work interface for managing transactions across repositories
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Repository properties
    IAssetRepository Assets { get; }
    ISegmentRepository Segments { get; }
    IMeasurementPointRepository MeasurementPoints { get; }
    IReadingRepository Readings { get; }
    IDefectRepository Defects { get; }
    IWorkOrderRepository WorkOrders { get; }
    IMaterialRepository Materials { get; }
    ICoatingRepository Coatings { get; }
    IFluidRepository Fluids { get; }

    // Transaction management
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base repository interface with common operations
/// </summary>
public interface IRepository<TEntity, TKey> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TKey> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for database connection management
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
    Task<IDbConnection> CreatePlantConnectionAsync(string plantCode, CancellationToken cancellationToken = default);
    string GetConnectionString();
    string GetPlantConnectionString(string plantCode);
}

/// <summary>
/// Interface for managing database schema operations
/// </summary>
public interface IDbSchemaManager
{
    Task InitializeCentralDatabaseAsync(CancellationToken cancellationToken = default);
    Task InitializePlantDatabaseAsync(string plantCode, CancellationToken cancellationToken = default);
    Task<bool> IsCentralDatabaseInitializedAsync(CancellationToken cancellationToken = default);
    Task<bool> IsPlantDatabaseInitializedAsync(string plantCode, CancellationToken cancellationToken = default);
    Task RunMigrationsAsync(CancellationToken cancellationToken = default);
}