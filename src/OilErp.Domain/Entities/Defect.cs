namespace OilErp.Domain.Entities;

/// <summary>
/// Represents a defect identified on an asset
/// </summary>
public class Defect
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AssetId { get; set; }
    public required string DefectType { get; set; }
    public required string Severity { get; set; }
    public string? Description { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DiscoveredBy { get; set; }
    public string? Location { get; set; }
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }

    // Navigation properties
    public Asset? Asset { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();

    // Business methods
    public void UpdateDescription(string description)
    {
        Description = description;
    }

    public void SetLocation(string location)
    {
        Location = location;
    }

    public void SetDiscoveredBy(string discoveredBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discoveredBy);
        DiscoveredBy = discoveredBy;
    }

    public void Resolve(string resolution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);
        
        IsResolved = true;
        ResolvedAt = DateTime.UtcNow;
        Resolution = resolution;
    }

    public void Reopen()
    {
        IsResolved = false;
        ResolvedAt = null;
        Resolution = null;
    }

    public bool IsCritical()
    {
        return Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsHigh()
    {
        return Severity.Equals("High", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsMedium()
    {
        return Severity.Equals("Medium", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsLow()
    {
        return Severity.Equals("Low", StringComparison.OrdinalIgnoreCase);
    }

    public TimeSpan GetAge()
    {
        return DateTime.UtcNow - DiscoveredAt;
    }

    public TimeSpan? GetResolutionTime()
    {
        return ResolvedAt.HasValue ? ResolvedAt.Value - DiscoveredAt : null;
    }

    public bool HasWorkOrders()
    {
        return WorkOrders.Any();
    }

    public int GetWorkOrderCount()
    {
        return WorkOrders.Count;
    }
}