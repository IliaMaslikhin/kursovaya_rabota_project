namespace OilErp.Domain.Entities;

/// <summary>
/// Represents a physical asset in the oil industry pipeline system
/// </summary>
public class Asset
{
    public required string Id { get; set; }
    public required string TagNumber { get; set; }
    public string? Description { get; set; }
    public required string PlantCode { get; set; }
    public string? AssetType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Segment> Segments { get; set; } = new List<Segment>();
    public ICollection<Defect> Defects { get; set; } = new List<Defect>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();

    // Business methods
    public void UpdateDescription(string description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddSegment(Segment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        
        if (segment.AssetId != Id)
            throw new InvalidOperationException("Segment must belong to this asset");
        
        Segments.Add(segment);
    }

    public void AddDefect(Defect defect)
    {
        ArgumentNullException.ThrowIfNull(defect);
        
        if (defect.AssetId != Id)
            throw new InvalidOperationException("Defect must belong to this asset");
        
        Defects.Add(defect);
    }

    public void AddWorkOrder(WorkOrder workOrder)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        
        if (workOrder.AssetId != Id)
            throw new InvalidOperationException("Work order must belong to this asset");
        
        WorkOrders.Add(workOrder);
    }

    public bool HasCriticalDefects()
    {
        return Defects.Any(d => d.Severity == "Critical");
    }

    public int GetSegmentCount()
    {
        return Segments.Count;
    }

    public decimal GetTotalLength()
    {
        return Segments.Sum(s => s.LengthM);
    }
}