using OilErp.Domain.Entities;

namespace OilErp.Domain.Interfaces;

/// <summary>
/// Repository interface for Asset entities
/// </summary>
public interface IAssetRepository
{
    // Basic CRUD operations
    Task<Asset?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Asset?> GetByTagNumberAsync(string tagNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<Asset>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Asset>> GetByPlantCodeAsync(string plantCode, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(Asset asset, CancellationToken cancellationToken = default);
    Task UpdateAsync(Asset asset, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<Asset>> GetByAssetTypeAsync(string assetType, CancellationToken cancellationToken = default);
    Task<IEnumerable<Asset>> GetAssetsWithCriticalDefectsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Asset>> GetAssetsWithOverdueWorkOrdersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Asset>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    // Asset with related data
    Task<Asset?> GetWithSegmentsAsync(string id, CancellationToken cancellationToken = default);
    Task<Asset?> GetWithDefectsAsync(string id, CancellationToken cancellationToken = default);
    Task<Asset?> GetWithWorkOrdersAsync(string id, CancellationToken cancellationToken = default);
    Task<Asset?> GetWithAllRelatedDataAsync(string id, CancellationToken cancellationToken = default);

    // Statistics
    Task<int> GetCountByPlantAsync(string plantCode, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Segment entities
/// </summary>
public interface ISegmentRepository
{
    Task<Segment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Segment>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Segment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Segment segment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Segment segment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<Segment>> GetByMaterialCodeAsync(string materialCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<Segment>> GetByCoatingCodeAsync(string coatingCode, CancellationToken cancellationToken = default);
    Task<Segment?> GetWithMeasurementPointsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalLengthByAssetAsync(string assetId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for MeasurementPoint entities
/// </summary>
public interface IMeasurementPointRepository
{
    Task<MeasurementPoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MeasurementPoint>> GetBySegmentIdAsync(Guid segmentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MeasurementPoint>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(MeasurementPoint measurementPoint, CancellationToken cancellationToken = default);
    Task UpdateAsync(MeasurementPoint measurementPoint, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<MeasurementPoint>> GetByMeasurementTypeAsync(string measurementType, CancellationToken cancellationToken = default);
    Task<MeasurementPoint?> GetWithReadingsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MeasurementPoint>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Reading entities
/// </summary>
public interface IReadingRepository
{
    Task<Reading?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Reading>> GetByPointIdAsync(Guid pointId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Reading>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Reading reading, CancellationToken cancellationToken = default);
    Task UpdateAsync(Reading reading, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<Reading>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<Reading>> GetByOperatorAsync(string operatorId, CancellationToken cancellationToken = default);
    Task<Reading?> GetLatestByPointIdAsync(Guid pointId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Reading>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Reading>> GetInvalidReadingsAsync(CancellationToken cancellationToken = default);
    
    // Statistics
    Task<decimal?> GetAverageByPointIdAsync(Guid pointId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<decimal?> GetMinValueByPointIdAsync(Guid pointId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<decimal?> GetMaxValueByPointIdAsync(Guid pointId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Defect entities
/// </summary>
public interface IDefectRepository
{
    Task<Defect?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Defect>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Defect>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Defect defect, CancellationToken cancellationToken = default);
    Task UpdateAsync(Defect defect, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<Defect>> GetBySeverityAsync(string severity, CancellationToken cancellationToken = default);
    Task<IEnumerable<Defect>> GetByDefectTypeAsync(string defectType, CancellationToken cancellationToken = default);
    Task<IEnumerable<Defect>> GetUnresolvedAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Defect>> GetResolvedAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Defect>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<Defect>> GetCriticalDefectsAsync(CancellationToken cancellationToken = default);
    
    // Statistics
    Task<int> GetCountBySeverityAsync(string severity, CancellationToken cancellationToken = default);
    Task<int> GetUnresolvedCountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkOrder entities
/// </summary>
public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkOrder?> GetByWoNumberAsync(string woNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Advanced queries
    Task<IEnumerable<WorkOrder>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetByWorkTypeAsync(string workType, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetByAssignedToAsync(string assignedTo, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetOverdueAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetScheduledForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetByPriorityAsync(string priority, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkOrder>> GetByDefectIdAsync(Guid defectId, CancellationToken cancellationToken = default);
    
    // Statistics
    Task<int> GetCountByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<int> GetOverdueCountAsync(CancellationToken cancellationToken = default);
}