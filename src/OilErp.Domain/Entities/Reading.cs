namespace OilErp.Domain.Entities;

/// <summary>
/// Represents an actual measurement reading taken at a measurement point
/// </summary>
public class Reading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid PointId { get; set; }
    public decimal Value { get; set; }
    public required string Unit { get; set; }
    public DateTime MeasuredAt { get; set; }
    public string? OperatorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public bool IsValid { get; set; } = true;

    // Navigation properties
    public MeasurementPoint? MeasurementPoint { get; set; }

    // Business methods
    public void UpdateValue(decimal value, string unit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        
        Value = value;
        Unit = unit;
    }

    public void SetOperator(string operatorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        OperatorId = operatorId;
    }

    public void AddNotes(string notes)
    {
        Notes = notes;
    }

    public void MarkAsInvalid(string reason)
    {
        IsValid = false;
        AddNotes($"Marked invalid: {reason}");
    }

    public void MarkAsValid()
    {
        IsValid = true;
    }

    public bool IsOutOfRange(decimal minValue, decimal maxValue)
    {
        return Value < minValue || Value > maxValue;
    }

    public bool IsMeasuredWithinLast(TimeSpan timeSpan)
    {
        return DateTime.UtcNow - MeasuredAt <= timeSpan;
    }

    public string GetFormattedValue()
    {
        return $"{Value:F2} {Unit}";
    }

    public bool IsMeasuredBefore(DateTime dateTime)
    {
        return MeasuredAt < dateTime;
    }

    public bool IsMeasuredAfter(DateTime dateTime)
    {
        return MeasuredAt > dateTime;
    }
}