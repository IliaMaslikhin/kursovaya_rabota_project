namespace OilErp.Domain.Entities;

/// <summary>
/// Represents a work order for maintenance or repair activities
/// </summary>
public class WorkOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AssetId { get; set; }
    public required string WoNumber { get; set; }
    public required string WorkType { get; set; }
    public required string Status { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string? AssignedTo { get; set; }
    public string? Priority { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public string? CompletionNotes { get; set; }

    // Navigation properties
    public Asset? Asset { get; set; }
    public Guid? DefectId { get; set; }
    public Defect? Defect { get; set; }

    // Business methods
    public void UpdateDescription(string description)
    {
        Description = description;
    }

    public void AssignTo(string operatorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        AssignedTo = operatorId;
    }

    public void SetPriority(string priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(priority);
        Priority = priority;
    }

    public void SetEstimatedHours(decimal hours)
    {
        if (hours <= 0)
            throw new ArgumentException("Estimated hours must be positive", nameof(hours));
        
        EstimatedHours = hours;
    }

    public void Start()
    {
        if (Status != "Scheduled")
            throw new InvalidOperationException("Work order must be scheduled to start");
        
        Status = "In Progress";
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(decimal actualHours, string completionNotes)
    {
        if (Status != "In Progress")
            throw new InvalidOperationException("Work order must be in progress to complete");
        
        if (actualHours <= 0)
            throw new ArgumentException("Actual hours must be positive", nameof(actualHours));
        
        Status = "Completed";
        CompletedAt = DateTime.UtcNow;
        ActualHours = actualHours;
        CompletionNotes = completionNotes;
    }

    public void Cancel(string reason)
    {
        if (Status == "Completed")
            throw new InvalidOperationException("Cannot cancel completed work order");
        
        Status = "Cancelled";
        CompletionNotes = $"Cancelled: {reason}";
    }

    public void Reschedule(DateTime newScheduledDate)
    {
        if (Status == "Completed")
            throw new InvalidOperationException("Cannot reschedule completed work order");
        
        ScheduledDate = newScheduledDate;
        
        if (Status == "In Progress")
            Status = "Scheduled";
    }

    public bool IsOverdue()
    {
        return Status != "Completed" && DateTime.UtcNow > ScheduledDate;
    }

    public bool IsInProgress()
    {
        return Status == "In Progress";
    }

    public bool IsCompleted()
    {
        return Status == "Completed";
    }

    public bool IsCancelled()
    {
        return Status == "Cancelled";
    }

    public TimeSpan? GetDuration()
    {
        if (StartedAt.HasValue && CompletedAt.HasValue)
            return CompletedAt.Value - StartedAt.Value;
        
        return null;
    }

    public decimal? GetHoursVariance()
    {
        if (EstimatedHours.HasValue && ActualHours.HasValue)
            return ActualHours.Value - EstimatedHours.Value;
        
        return null;
    }
}