using System.Data;
using OilErp.Domain.Interfaces;
using OilErp.Data.Repositories;

namespace OilErp.Data;

/// <summary>
/// Unit of Work implementation for managing transactions and repositories
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbTransaction? _transaction;
    private IDbConnection? _connection;

    // Repository instances
    private IAssetRepository? _assets;
    private ISegmentRepository? _segments;
    private IMeasurementPointRepository? _measurementPoints;
    private IReadingRepository? _readings;
    private IDefectRepository? _defects;
    private IWorkOrderRepository? _workOrders;
    private IMaterialRepository? _materials;
    private ICoatingRepository? _coatings;
    private IFluidRepository? _fluids;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // Repository properties - create instances on demand
    public IAssetRepository Assets => _assets ??= new AssetRepository(_connectionFactory);
    public ISegmentRepository Segments => _segments ??= new SegmentRepository(_connectionFactory);
    public IMeasurementPointRepository MeasurementPoints => _measurementPoints ??= new MeasurementPointRepository(_connectionFactory);
    public IReadingRepository Readings => _readings ??= new ReadingRepository(_connectionFactory);
    public IDefectRepository Defects => _defects ??= new DefectRepository(_connectionFactory);
    public IWorkOrderRepository WorkOrders => _workOrders ??= new WorkOrderRepository(_connectionFactory);
    public IMaterialRepository Materials => _materials ??= new MaterialRepository(_connectionFactory);
    public ICoatingRepository Coatings => _coatings ??= new CoatingRepository(_connectionFactory);
    public IFluidRepository Fluids => _fluids ??= new FluidRepository(_connectionFactory);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Transaction already started");
        }

        _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        _transaction = _connection.BeginTransaction();
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction to commit");
        }

        try
        {
            _transaction.Commit();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
            
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }
        
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction to rollback");
        }

        try
        {
            _transaction.Rollback();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
            
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }
        
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // In a Dapper implementation, changes are saved immediately
        // This method is mainly for EF compatibility
        return Task.FromResult(0);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }
}