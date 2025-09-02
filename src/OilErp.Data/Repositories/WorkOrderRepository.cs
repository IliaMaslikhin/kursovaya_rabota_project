using System.Data;
using Dapper;
using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Repository implementation for WorkOrder entities with workflow management
/// </summary>
public class WorkOrderRepository : BaseRepository<WorkOrder, Guid>, IWorkOrderRepository
{
    public WorkOrderRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<WorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.id = @Id";

        return await QuerySingleOrDefaultAsync<WorkOrder>(sql, new { Id = id }, cancellationToken);
    }

    public override async Task<IEnumerable<WorkOrder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            ORDER BY wo.scheduled_date DESC";

        return await QueryAsync<WorkOrder>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<Guid> CreateAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        // Validate that asset exists
        const string validateAssetSql = @"
            SELECT COUNT(*) 
            FROM assets.global_assets 
            WHERE id = @AssetId";

        var assetExists = await ExecuteScalarAsync<int>(validateAssetSql, new { workOrder.AssetId }, cancellationToken) > 0;
        
        if (!assetExists)
        {
            throw new InvalidOperationException($"Asset with ID '{workOrder.AssetId}' does not exist");
        }

        // Validate that WO number is unique
        const string validateWoNumberSql = @"
            SELECT COUNT(*) 
            FROM work_orders 
            WHERE wo_number = @WoNumber";

        var woNumberExists = await ExecuteScalarAsync<int>(validateWoNumberSql, new { workOrder.WoNumber }, cancellationToken) > 0;
        
        if (woNumberExists)
        {
            throw new InvalidOperationException($"Work order with number '{workOrder.WoNumber}' already exists");
        }

        // Validate defect exists if provided
        if (workOrder.DefectId.HasValue)
        {
            const string validateDefectSql = @"
                SELECT COUNT(*) 
                FROM defects 
                WHERE id = @DefectId";

            var defectExists = await ExecuteScalarAsync<int>(validateDefectSql, new { workOrder.DefectId }, cancellationToken) > 0;
            
            if (!defectExists)
            {
                throw new InvalidOperationException($"Defect with ID '{workOrder.DefectId}' does not exist");
            }
        }

        const string sql = @"
            INSERT INTO work_orders (id, asset_id, wo_number, work_type, status, scheduled_date, 
                                   started_at, completed_at, created_at, description, assigned_to,
                                   priority, estimated_hours, actual_hours, completion_notes, defect_id)
            VALUES (@Id, @AssetId, @WoNumber, @WorkType, @Status, @ScheduledDate, 
                   @StartedAt, @CompletedAt, @CreatedAt, @Description, @AssignedTo,
                   @Priority, @EstimatedHours, @ActualHours, @CompletionNotes, @DefectId)
            RETURNING id";

        if (workOrder.Id == Guid.Empty)
            workOrder.Id = Guid.NewGuid();

        workOrder.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            workOrder.Id,
            workOrder.AssetId,
            workOrder.WoNumber,
            workOrder.WorkType,
            workOrder.Status,
            workOrder.ScheduledDate,
            workOrder.StartedAt,
            workOrder.CompletedAt,
            workOrder.CreatedAt,
            workOrder.Description,
            workOrder.AssignedTo,
            workOrder.Priority,
            workOrder.EstimatedHours,
            workOrder.ActualHours,
            workOrder.CompletionNotes,
            workOrder.DefectId
        };

        var result = await ExecuteScalarAsync<Guid>(sql, parameters, cancellationToken);
        return result;
    }

    public override async Task UpdateAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        // Check if WO number is being changed and ensure uniqueness
        const string currentWoNumberSql = @"
            SELECT wo_number 
            FROM work_orders 
            WHERE id = @Id";

        var currentWoNumber = await ExecuteScalarAsync<string>(currentWoNumberSql, new { workOrder.Id }, cancellationToken);
        
        if (currentWoNumber != workOrder.WoNumber)
        {
            const string validateWoNumberSql = @"
                SELECT COUNT(*) 
                FROM work_orders 
                WHERE wo_number = @WoNumber AND id != @Id";

            var woNumberExists = await ExecuteScalarAsync<int>(validateWoNumberSql, 
                new { workOrder.WoNumber, workOrder.Id }, cancellationToken) > 0;
            
            if (woNumberExists)
            {
                throw new InvalidOperationException($"Work order with number '{workOrder.WoNumber}' already exists");
            }
        }

        const string sql = @"
            UPDATE work_orders 
            SET wo_number = @WoNumber,
                work_type = @WorkType,
                status = @Status,
                scheduled_date = @ScheduledDate,
                started_at = @StartedAt,
                completed_at = @CompletedAt,
                description = @Description,
                assigned_to = @AssignedTo,
                priority = @Priority,
                estimated_hours = @EstimatedHours,
                actual_hours = @ActualHours,
                completion_notes = @CompletionNotes,
                defect_id = @DefectId
            WHERE id = @Id";

        var parameters = new
        {
            workOrder.Id,
            workOrder.WoNumber,
            workOrder.WorkType,
            workOrder.Status,
            workOrder.ScheduledDate,
            workOrder.StartedAt,
            workOrder.CompletedAt,
            workOrder.Description,
            workOrder.AssignedTo,
            workOrder.Priority,
            workOrder.EstimatedHours,
            workOrder.ActualHours,
            workOrder.CompletionNotes,
            workOrder.DefectId
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Work order with ID '{workOrder.Id}' not found for update");
        }
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Check if work order is in progress or completed
        const string checkStatusSql = @"
            SELECT status 
            FROM work_orders 
            WHERE id = @Id";

        var status = await ExecuteScalarAsync<string>(checkStatusSql, new { Id = id }, cancellationToken);
        
        if (status == "In Progress" || status == "Completed")
        {
            throw new InvalidOperationException($"Cannot delete work order with status '{status}'. Only draft or cancelled work orders can be deleted.");
        }

        const string sql = "DELETE FROM work_orders WHERE id = @Id";
        
        var affectedRows = await ExecuteAsync(sql, new { Id = id }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Work order with ID '{id}' not found for deletion");
        }
    }

    public async Task<WorkOrder?> GetByWoNumberAsync(string woNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(woNumber);

        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.wo_number = @WoNumber";

        return await QuerySingleOrDefaultAsync<WorkOrder>(sql, new { WoNumber = woNumber }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.asset_id = @AssetId
            ORDER BY wo.scheduled_date DESC";

        return await QueryAsync<WorkOrder>(sql, new { AssetId = assetId }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.status = @Status
            ORDER BY wo.scheduled_date ASC";

        return await QueryAsync<WorkOrder>(sql, new { Status = status }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetByWorkTypeAsync(string workType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workType);

        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.work_type = @WorkType
            ORDER BY wo.scheduled_date DESC";

        return await QueryAsync<WorkOrder>(sql, new { WorkType = workType }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetByAssignedToAsync(string assignedTo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedTo);

        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.assigned_to = @AssignedTo
            ORDER BY wo.scheduled_date ASC";

        return await QueryAsync<WorkOrder>(sql, new { AssignedTo = assignedTo }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.status NOT IN ('Completed', 'Cancelled') 
            AND wo.scheduled_date < @CurrentTime
            ORDER BY wo.scheduled_date ASC";

        return await QueryAsync<WorkOrder>(sql, new { CurrentTime = DateTime.UtcNow }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetScheduledForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE DATE(wo.scheduled_date) = DATE(@Date)
            ORDER BY wo.scheduled_date ASC";

        return await QueryAsync<WorkOrder>(sql, new { Date = date }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetByPriorityAsync(string priority, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(priority);

        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.priority = @Priority
            ORDER BY wo.scheduled_date ASC";

        return await QueryAsync<WorkOrder>(sql, new { Priority = priority }, cancellationToken);
    }

    public async Task<IEnumerable<WorkOrder>> GetByDefectIdAsync(Guid defectId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT wo.id, wo.asset_id as AssetId, wo.wo_number as WoNumber,
                   wo.work_type as WorkType, wo.status as Status,
                   wo.scheduled_date as ScheduledDate, wo.started_at as StartedAt,
                   wo.completed_at as CompletedAt, wo.created_at as CreatedAt,
                   wo.description as Description, wo.assigned_to as AssignedTo,
                   wo.priority as Priority, wo.estimated_hours as EstimatedHours,
                   wo.actual_hours as ActualHours, wo.completion_notes as CompletionNotes,
                   wo.defect_id as DefectId
            FROM work_orders wo 
            WHERE wo.defect_id = @DefectId
            ORDER BY wo.created_at DESC";

        return await QueryAsync<WorkOrder>(sql, new { DefectId = defectId }, cancellationToken);
    }

    public async Task<int> GetCountByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        const string sql = @"
            SELECT COUNT(*) 
            FROM work_orders 
            WHERE status = @Status";

        return await ExecuteScalarAsync<int>(sql, new { Status = status }, cancellationToken);
    }

    public async Task<int> GetOverdueCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM work_orders 
            WHERE status NOT IN ('Completed', 'Cancelled') 
            AND scheduled_date < @CurrentTime";

        return await ExecuteScalarAsync<int>(sql, new { CurrentTime = DateTime.UtcNow }, cancellationToken);
    }
}