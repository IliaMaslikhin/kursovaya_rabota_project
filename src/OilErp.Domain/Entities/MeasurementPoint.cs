namespace OilErp.Domain.Entities;

/// <summary>
/// Represents a specific point on a segment where measurements are taken
/// </summary>
public class MeasurementPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SegmentId { get; set; }
    public required string PointName { get; set; }
    public decimal DistanceFromStart { get; set; }
    public required string MeasurementType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Segment? Segment { get; set; }
    public ICollection<Reading> Readings { get; set; } = new List<Reading>();

    // Business methods
    public void UpdatePosition(decimal distanceFromStart)
    {
        if (distanceFromStart < 0)
            throw new ArgumentException("Distance from start cannot be negative", nameof(distanceFromStart));
        
        DistanceFromStart = distanceFromStart;
    }

    public void SetMeasurementType(string measurementType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(measurementType);
        MeasurementType = measurementType;
    }

    public void AddReading(Reading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        
        if (reading.PointId != Id)
            throw new InvalidOperationException("Reading must belong to this measurement point");
        
        Readings.Add(reading);
    }

    public Reading? GetLatestReading()
    {
        return Readings
            .OrderByDescending(r => r.MeasuredAt)
            .FirstOrDefault();
    }

    public IEnumerable<Reading> GetReadingsInDateRange(DateTime fromDate, DateTime toDate)
    {
        return Readings
            .Where(r => r.MeasuredAt >= fromDate && r.MeasuredAt <= toDate)
            .OrderByDescending(r => r.MeasuredAt);
    }

    public decimal? GetAverageReading(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var readings = Readings.AsQueryable();
        
        if (fromDate.HasValue)
            readings = readings.Where(r => r.MeasuredAt >= fromDate.Value);
        
        if (toDate.HasValue)
            readings = readings.Where(r => r.MeasuredAt <= toDate.Value);
        
        return readings.Any() ? readings.Average(r => r.Value) : null;
    }

    public int GetReadingCount()
    {
        return Readings.Count;
    }
}