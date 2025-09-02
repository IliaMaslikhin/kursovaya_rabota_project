using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;
using OilErp.Domain.ValueObjects;

namespace OilErp.Domain.Services;

/// <summary>
/// Domain service for measurement operations and data validation
/// </summary>
public class MeasurementService
{
    private readonly IUnitOfWork _unitOfWork;

    public MeasurementService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Records a new measurement reading with validation
    /// </summary>
    public async Task<Guid> RecordReadingAsync(Reading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        // Validate measurement point exists
        var measurementPoint = await _unitOfWork.MeasurementPoints.GetByIdAsync(reading.PointId, cancellationToken);
        if (measurementPoint == null)
        {
            throw new InvalidOperationException($"Measurement point with ID '{reading.PointId}' not found");
        }

        // Set creation time
        reading.CreatedAt = DateTime.UtcNow;

        // Validate reading value based on measurement type
        ValidateReadingValue(reading, measurementPoint.MeasurementType);

        // Check for potential anomalies
        await CheckForAnomaliesAsync(reading, cancellationToken);

        return await _unitOfWork.Readings.CreateAsync(reading, cancellationToken);
    }

    /// <summary>
    /// Validates a reading value based on measurement type
    /// </summary>
    private static void ValidateReadingValue(Reading reading, string measurementType)
    {
        switch (measurementType.ToLower())
        {
            case "wallthickness":
                if (reading.Value <= 0)
                    throw new ArgumentException("Wall thickness must be positive");
                if (reading.Value > 50) // Reasonable upper limit for mm
                    reading.AddNotes("Warning: Unusually high wall thickness reading");
                break;

            case "pressure":
                if (reading.Value < 0)
                    throw new ArgumentException("Pressure cannot be negative");
                break;

            case "temperature":
                if (reading.Unit.ToLower().Contains("celsius"))
                {
                    if (reading.Value < -273.15m)
                        throw new ArgumentException("Temperature cannot be below absolute zero");
                }
                break;

            case "pitdepth":
                if (reading.Value < 0)
                    throw new ArgumentException("Pit depth cannot be negative");
                break;
        }
    }

    /// <summary>
    /// Checks for anomalies in the reading compared to historical data
    /// </summary>
    private async Task CheckForAnomaliesAsync(Reading reading, CancellationToken cancellationToken)
    {
        // Get recent readings for comparison
        var recentReadings = await _unitOfWork.Readings.GetByPointIdAsync(reading.PointId, cancellationToken);
        var last10Readings = recentReadings
            .OrderByDescending(r => r.MeasuredAt)
            .Take(10)
            .Where(r => r.IsValid)
            .ToList();

        if (last10Readings.Count < 3)
            return; // Not enough data for anomaly detection

        var average = last10Readings.Average(r => r.Value);
        var standardDeviation = CalculateStandardDeviation(last10Readings.Select(r => r.Value));

        // Flag as potential anomaly if reading is more than 2 standard deviations from mean
        var deviationFromMean = Math.Abs(reading.Value - average);
        if (deviationFromMean > 2 * standardDeviation)
        {
            reading.AddNotes($"Potential anomaly detected: Value deviates {deviationFromMean:F2} from recent average of {average:F2}");
        }
    }

    /// <summary>
    /// Calculates standard deviation for a set of values
    /// </summary>
    private static decimal CalculateStandardDeviation(IEnumerable<decimal> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count <= 1)
            return 0;

        var average = valuesList.Average();
        var sumOfSquaresOfDifferences = valuesList.Sum(val => (val - average) * (val - average));
        var variance = sumOfSquaresOfDifferences / valuesList.Count;
        return (decimal)Math.Sqrt((double)variance);
    }

    /// <summary>
    /// Gets measurement trend analysis for a measurement point
    /// </summary>
    public async Task<MeasurementTrend> GetMeasurementTrendAsync(Guid pointId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var readings = await _unitOfWork.Readings.GetByPointIdAsync(pointId, cancellationToken);
        var filteredReadings = readings
            .Where(r => r.MeasuredAt >= fromDate && r.MeasuredAt <= toDate && r.IsValid)
            .OrderBy(r => r.MeasuredAt)
            .ToList();

        if (!filteredReadings.Any())
        {
            return new MeasurementTrend
            {
                PointId = pointId,
                FromDate = fromDate,
                ToDate = toDate,
                ReadingCount = 0,
                TrendDirection = "No Data"
            };
        }

        var firstValue = filteredReadings.First().Value;
        var lastValue = filteredReadings.Last().Value;
        var averageValue = filteredReadings.Average(r => r.Value);
        var minValue = filteredReadings.Min(r => r.Value);
        var maxValue = filteredReadings.Max(r => r.Value);

        var trendDirection = lastValue > firstValue ? "Increasing" :
                            lastValue < firstValue ? "Decreasing" : "Stable";

        var changeRate = filteredReadings.Count > 1 ? 
            (lastValue - firstValue) / (decimal)(toDate - fromDate).TotalDays : 0;

        return new MeasurementTrend
        {
            PointId = pointId,
            FromDate = fromDate,
            ToDate = toDate,
            ReadingCount = filteredReadings.Count,
            AverageValue = averageValue,
            MinValue = minValue,
            MaxValue = maxValue,
            FirstValue = firstValue,
            LastValue = lastValue,
            TrendDirection = trendDirection,
            ChangeRate = changeRate
        };
    }

    /// <summary>
    /// Creates a measurement point with validation
    /// </summary>
    public async Task<Guid> CreateMeasurementPointAsync(MeasurementPoint measurementPoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(measurementPoint);

        // Validate segment exists
        var segment = await _unitOfWork.Segments.GetByIdAsync(measurementPoint.SegmentId, cancellationToken);
        if (segment == null)
        {
            throw new InvalidOperationException($"Segment with ID '{measurementPoint.SegmentId}' not found");
        }

        // Validate position within segment
        if (measurementPoint.DistanceFromStart > segment.LengthM)
        {
            throw new ArgumentException("Measurement point distance cannot exceed segment length");
        }

        measurementPoint.CreatedAt = DateTime.UtcNow;

        return await _unitOfWork.MeasurementPoints.CreateAsync(measurementPoint, cancellationToken);
    }

    /// <summary>
    /// Gets readings that exceed specified thresholds
    /// </summary>
    public async Task<IEnumerable<Reading>> GetReadingsExceedingThresholdAsync(RiskThreshold threshold, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var allReadings = await _unitOfWork.Readings.GetByDateRangeAsync(fromDate, toDate, cancellationToken);
        
        return allReadings.Where(r => 
            r.IsValid && 
            threshold.ExceedsThreshold(r.Value, "critical"));
    }
}

/// <summary>
/// Data transfer object for measurement trend analysis
/// </summary>
public record MeasurementTrend
{
    public Guid PointId { get; init; }
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public int ReadingCount { get; init; }
    public decimal AverageValue { get; init; }
    public decimal MinValue { get; init; }
    public decimal MaxValue { get; init; }
    public decimal FirstValue { get; init; }
    public decimal LastValue { get; init; }
    public required string TrendDirection { get; init; }
    public decimal ChangeRate { get; init; }
}