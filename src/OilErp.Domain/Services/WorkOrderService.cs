using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Domain.Services;

/// <summary>
/// Domain service for work order management and scheduling
/// </summary>
public class WorkOrderService
{
    private readonly IUnitOfWork _unitOfWork;

    public WorkOrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Creates a new work order with validation
    /// </summary>
    public async Task<Guid> CreateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        // Validate asset exists
        var asset = await _unitOfWork.Assets.GetByIdAsync(workOrder.AssetId, cancellationToken);
        if (asset == null)
        {
            throw new InvalidOperationException($"Asset with ID '{workOrder.AssetId}' not found");
        }

        // Ensure unique work order number
        var existingWo = await _unitOfWork.WorkOrders.GetByWoNumberAsync(workOrder.WoNumber, cancellationToken);
        if (existingWo != null)
        {
            throw new InvalidOperationException($"Work order with number '{workOrder.WoNumber}' already exists");
        }

        // Validate defect if specified
        if (workOrder.DefectId.HasValue)
        {
            var defect = await _unitOfWork.Defects.GetByIdAsync(workOrder.DefectId.Value, cancellationToken);
            if (defect == null)
            {
                throw new InvalidOperationException($"Defect with ID '{workOrder.DefectId}' not found");
            }

            if (defect.AssetId != workOrder.AssetId)
            {
                throw new InvalidOperationException("Defect must belong to the same asset as the work order");
            }
        }

        workOrder.CreatedAt = DateTime.UtcNow;
        workOrder.Status = "Scheduled";

        return await _unitOfWork.WorkOrders.CreateAsync(workOrder, cancellationToken);
    }

    /// <summary>
    /// Creates work orders based on defect severity
    /// </summary>
    public async Task<Guid> CreateWorkOrderFromDefectAsync(Guid defectId, string workType = "Corrective", CancellationToken cancellationToken = default)
    {
        var defect = await _unitOfWork.Defects.GetByIdAsync(defectId, cancellationToken);
        if (defect == null)
        {
            throw new InvalidOperationException($"Defect with ID '{defectId}' not found");
        }

        // Generate work order number
        var woNumber = await GenerateWorkOrderNumberAsync(cancellationToken);

        // Determine priority and scheduled date based on defect severity
        var (priority, scheduledDate) = GetPriorityAndScheduleFromSeverity(defect.Severity);

        var workOrder = new WorkOrder
        {
            AssetId = defect.AssetId,
            WoNumber = woNumber,
            WorkType = workType,
            Status = "Scheduled",
            ScheduledDate = scheduledDate,
            Description = $"Address {defect.Severity.ToLower()} {defect.DefectType.ToLower()} defect: {defect.Description}",
            Priority = priority,
            DefectId = defectId
        };

        return await CreateWorkOrderAsync(workOrder, cancellationToken);
    }

    /// <summary>
    /// Schedules preventive maintenance for an asset
    /// </summary>
    public async Task<List<Guid>> SchedulePreventiveMaintenanceAsync(string assetId, DateTime startDate, int intervalDays, int count, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var asset = await _unitOfWork.Assets.GetByIdAsync(assetId, cancellationToken);
        if (asset == null)
        {
            throw new InvalidOperationException($"Asset with ID '{assetId}' not found");
        }

        var workOrderIds = new List<Guid>();
        var currentDate = startDate;

        for (int i = 0; i < count; i++)
        {
            var woNumber = await GenerateWorkOrderNumberAsync(cancellationToken);
            
            var workOrder = new WorkOrder
            {
                AssetId = assetId,
                WoNumber = woNumber,
                WorkType = "Preventive",
                Status = "Scheduled",
                ScheduledDate = currentDate,
                Description = $"Scheduled preventive maintenance for {asset.TagNumber}",
                Priority = "Medium",
                EstimatedHours = 8.0m // Default estimate
            };

            var workOrderId = await CreateWorkOrderAsync(workOrder, cancellationToken);
            workOrderIds.Add(workOrderId);

            currentDate = currentDate.AddDays(intervalDays);
        }

        return workOrderIds;
    }

    /// <summary>
    /// Gets work order scheduling recommendations
    /// </summary>
    public async Task<WorkOrderSchedulingRecommendations> GetSchedulingRecommendationsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var scheduledWorkOrders = await _unitOfWork.WorkOrders.GetByStatusAsync("Scheduled", cancellationToken);
        var overdueWorkOrders = await _unitOfWork.WorkOrders.GetOverdueAsync(cancellationToken);
        var inProgressWorkOrders = await _unitOfWork.WorkOrders.GetByStatusAsync("In Progress", cancellationToken);

        var workOrdersInPeriod = scheduledWorkOrders
            .Where(w => w.ScheduledDate >= fromDate && w.ScheduledDate <= toDate)
            .OrderBy(w => w.ScheduledDate)
            .ToList();

        // Group by date for workload analysis
        var dailyWorkload = workOrdersInPeriod
            .GroupBy(w => w.ScheduledDate.Date)
            .ToDictionary(
                g => g.Key,
                g => new DailyWorkload
                {
                    Date = g.Key,
                    WorkOrderCount = g.Count(),
                    EstimatedHours = g.Sum(w => w.EstimatedHours ?? 8.0m),
                    EmergencyCount = g.Count(w => w.Priority == "Emergency"),
                    HighPriorityCount = g.Count(w => w.Priority == "High")
                });

        // Identify overloaded days (more than 40 hours of work)
        var overloadedDays = dailyWorkload
            .Where(kvp => kvp.Value.EstimatedHours > 40)
            .Select(kvp => kvp.Key)
            .ToList();

        // Priority recommendations
        var priorityRecommendations = new List<string>();

        if (overdueWorkOrders.Any())
        {
            priorityRecommendations.Add($"Complete {overdueWorkOrders.Count()} overdue work orders immediately");
        }

        var emergencyWo = workOrdersInPeriod.Where(w => w.Priority == "Emergency").ToList();
        if (emergencyWo.Any())
        {
            priorityRecommendations.Add($"Schedule {emergencyWo.Count} emergency work orders within 24 hours");
        }

        if (overloadedDays.Any())
        {
            priorityRecommendations.Add($"Redistribute workload on {overloadedDays.Count} overloaded days");
        }

        return new WorkOrderSchedulingRecommendations
        {
            PeriodStart = fromDate,
            PeriodEnd = toDate,
            TotalScheduledWorkOrders = workOrdersInPeriod.Count,
            OverdueWorkOrders = overdueWorkOrders.Count(),
            InProgressWorkOrders = inProgressWorkOrders.Count(),
            DailyWorkloads = dailyWorkload,
            OverloadedDays = overloadedDays,
            Recommendations = priorityRecommendations
        };
    }

    /// <summary>
    /// Generates a unique work order number
    /// </summary>
    private async Task<string> GenerateWorkOrderNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = "WO";
        var year = DateTime.UtcNow.Year;
        var counter = 1;

        string woNumber;
        WorkOrder? existingWo;

        do
        {
            woNumber = $"{prefix}{year:D4}{counter:D6}";
            existingWo = await _unitOfWork.WorkOrders.GetByWoNumberAsync(woNumber, cancellationToken);
            counter++;
        } 
        while (existingWo != null);

        return woNumber;
    }

    /// <summary>
    /// Determines priority and schedule based on defect severity
    /// </summary>
    private static (string Priority, DateTime ScheduledDate) GetPriorityAndScheduleFromSeverity(string severity)
    {
        var now = DateTime.UtcNow;
        
        return severity.ToLower() switch
        {
            "critical" => ("Emergency", now.AddHours(4)), // Within 4 hours
            "high" => ("High", now.AddDays(1)), // Within 1 day
            "medium" => ("Medium", now.AddDays(7)), // Within 1 week
            "low" => ("Low", now.AddDays(30)), // Within 1 month
            _ => ("Medium", now.AddDays(14)) // Default: 2 weeks
        };
    }
}

/// <summary>
/// Data transfer object for work order scheduling recommendations
/// </summary>
public record WorkOrderSchedulingRecommendations
{
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int TotalScheduledWorkOrders { get; init; }
    public int OverdueWorkOrders { get; init; }
    public int InProgressWorkOrders { get; init; }
    public required Dictionary<DateTime, DailyWorkload> DailyWorkloads { get; init; }
    public required List<DateTime> OverloadedDays { get; init; }
    public required List<string> Recommendations { get; init; }
}

/// <summary>
/// Represents daily workload information
/// </summary>
public record DailyWorkload
{
    public DateTime Date { get; init; }
    public int WorkOrderCount { get; init; }
    public decimal EstimatedHours { get; init; }
    public int EmergencyCount { get; init; }
    public int HighPriorityCount { get; init; }
}