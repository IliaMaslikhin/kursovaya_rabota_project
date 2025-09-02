namespace OilErp.Domain.Entities;

/// <summary>
/// Represents a segment or portion of an asset with specific material and coating properties
/// </summary>
public class Segment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AssetId { get; set; }
    public required string SegmentName { get; set; }
    public decimal LengthM { get; set; }
    public string? MaterialCode { get; set; }
    public string? CoatingCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Asset? Asset { get; set; }
    public ICollection<MeasurementPoint> MeasurementPoints { get; set; } = new List<MeasurementPoint>();

    // Business methods
    public void UpdateLength(decimal lengthM)
    {
        if (lengthM <= 0)
            throw new ArgumentException("Segment length must be positive", nameof(lengthM));
        
        LengthM = lengthM;
    }

    public void SetMaterial(string materialCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialCode);
        MaterialCode = materialCode;
    }

    public void SetCoating(string coatingCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coatingCode);
        CoatingCode = coatingCode;
    }

    public void AddMeasurementPoint(MeasurementPoint measurementPoint)
    {
        ArgumentNullException.ThrowIfNull(measurementPoint);
        
        if (measurementPoint.SegmentId != Id)
            throw new InvalidOperationException("Measurement point must belong to this segment");
        
        if (measurementPoint.DistanceFromStart > LengthM)
            throw new InvalidOperationException("Measurement point cannot be beyond segment length");
        
        MeasurementPoints.Add(measurementPoint);
    }

    public int GetMeasurementPointCount()
    {
        return MeasurementPoints.Count;
    }

    public IEnumerable<MeasurementPoint> GetMeasurementPointsByType(string measurementType)
    {
        return MeasurementPoints.Where(mp => mp.MeasurementType == measurementType);
    }
}