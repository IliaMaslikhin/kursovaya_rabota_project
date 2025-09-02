using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;
using OilErp.Domain.ValueObjects;

namespace OilErp.Domain.Services;

/// <summary>
/// Domain service for risk assessment and management
/// </summary>
public class RiskAssessmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeasurementService _measurementService;

    public RiskAssessmentService(IUnitOfWork unitOfWork, MeasurementService measurementService)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _measurementService = measurementService ?? throw new ArgumentNullException(nameof(measurementService));
    }

    /// <summary>
    /// Assesses risk for an asset based on current measurements and defects
    /// </summary>
    public async Task<RiskAssessmentResult> AssessAssetRiskAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var asset = await _unitOfWork.Assets.GetWithAllRelatedDataAsync(assetId, cancellationToken);
        if (asset == null)
        {
            throw new InvalidOperationException($"Asset with ID '{assetId}' not found");
        }

        var riskFactors = new List<RiskFactor>();

        // Assess defect-related risks
        await AssessDefectRisksAsync(asset, riskFactors, cancellationToken);

        // Assess measurement-related risks
        await AssessMeasurementRisksAsync(asset, riskFactors, cancellationToken);

        // Assess maintenance-related risks
        await AssessMaintenanceRisksAsync(asset, riskFactors, cancellationToken);

        // Calculate overall risk score
        var overallRiskScore = CalculateOverallRiskScore(riskFactors);
        var riskLevel = DetermineRiskLevel(overallRiskScore);

        return new RiskAssessmentResult
        {
            AssetId = assetId,
            AssessmentDate = DateTime.UtcNow,
            OverallRiskScore = overallRiskScore,
            RiskLevel = riskLevel,
            RiskFactors = riskFactors,
            Recommendations = GenerateRecommendations(riskFactors, riskLevel)
        };
    }

    /// <summary>
    /// Assesses risks related to defects
    /// </summary>
    private async Task AssessDefectRisksAsync(Asset asset, List<RiskFactor> riskFactors, CancellationToken cancellationToken)
    {
        var unresolvedDefects = asset.Defects.Where(d => !d.IsResolved).ToList();

        foreach (var defect in unresolvedDefects)
        {
            var riskScore = defect.Severity switch
            {
                "Critical" => 100,
                "High" => 75,
                "Medium" => 50,
                "Low" => 25,
                _ => 10
            };

            // Increase risk score based on defect age
            var ageInDays = (DateTime.UtcNow - defect.DiscoveredAt).TotalDays;
            if (ageInDays > 30) riskScore += 10;
            if (ageInDays > 90) riskScore += 20;

            riskFactors.Add(new RiskFactor
            {
                Category = "Defect",
                Description = $"{defect.Severity} {defect.DefectType} defect aged {ageInDays:F0} days",
                RiskScore = riskScore,
                Severity = defect.Severity,
                Details = defect.Description ?? ""
            });
        }
    }

    /// <summary>
    /// Assesses risks related to measurements
    /// </summary>
    private async Task AssessMeasurementRisksAsync(Asset asset, List<RiskFactor> riskFactors, CancellationToken cancellationToken)
    {
        var wallThicknessThreshold = new RiskThreshold(
            "Wall Thickness",
            8.0m, 6.0m, 4.0m, 2.0m,
            "mm",
            "Minimum wall thickness requirements"
        );

        foreach (var segment in asset.Segments)
        {
            foreach (var point in segment.MeasurementPoints.Where(mp => mp.MeasurementType == "WallThickness"))
            {
                var latestReading = await _unitOfWork.Readings.GetLatestByPointIdAsync(point.Id, cancellationToken);
                
                if (latestReading != null && latestReading.IsValid)
                {
                    var riskLevel = wallThicknessThreshold.GetRiskLevel(latestReading.Value);
                    
                    if (riskLevel != "Normal")
                    {
                        var riskScore = riskLevel switch
                        {
                            "Critical" => 95,
                            "High" => 70,
                            "Medium" => 45,
                            "Low" => 20,
                            _ => 0
                        };

                        riskFactors.Add(new RiskFactor
                        {
                            Category = "Measurement",
                            Description = $"Wall thickness {latestReading.Value} {latestReading.Unit} at {point.PointName}",
                            RiskScore = riskScore,
                            Severity = riskLevel,
                            Details = $"Measurement taken on {latestReading.MeasuredAt:yyyy-MM-dd}"
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Assesses risks related to maintenance
    /// </summary>
    private async Task AssessMaintenanceRisksAsync(Asset asset, List<RiskFactor> riskFactors, CancellationToken cancellationToken)
    {
        var overdueWorkOrders = asset.WorkOrders.Where(w => w.IsOverdue()).ToList();
        
        foreach (var workOrder in overdueWorkOrders)
        {
            var overdueDays = (DateTime.UtcNow - workOrder.ScheduledDate).TotalDays;
            var riskScore = workOrder.Priority switch
            {
                "Emergency" => 90 + (int)(overdueDays * 2),
                "High" => 60 + (int)(overdueDays * 1.5),
                "Medium" => 30 + (int)overdueDays,
                "Low" => 15 + (int)(overdueDays * 0.5),
                _ => 10 + (int)(overdueDays * 0.5)
            };

            riskFactors.Add(new RiskFactor
            {
                Category = "Maintenance",
                Description = $"Overdue {workOrder.WorkType} work order (WO: {workOrder.WoNumber})",
                RiskScore = Math.Min(riskScore, 100), // Cap at 100
                Severity = workOrder.Priority ?? "Medium",
                Details = $"Scheduled for {workOrder.ScheduledDate:yyyy-MM-dd}, overdue by {overdueDays:F0} days"
            });
        }

        // Check for lack of recent inspections
        var lastInspectionWorkOrder = asset.WorkOrders
            .Where(w => w.WorkType == "Inspection" && w.IsCompleted())
            .OrderByDescending(w => w.CompletedAt)
            .FirstOrDefault();

        if (lastInspectionWorkOrder == null || 
            (DateTime.UtcNow - lastInspectionWorkOrder.CompletedAt!.Value).TotalDays > 365)
        {
            riskFactors.Add(new RiskFactor
            {
                Category = "Maintenance",
                Description = "No recent inspection records",
                RiskScore = 40,
                Severity = "Medium",
                Details = lastInspectionWorkOrder != null 
                    ? $"Last inspection: {lastInspectionWorkOrder.CompletedAt:yyyy-MM-dd}"
                    : "No inspection history found"
            });
        }
    }

    /// <summary>
    /// Calculates overall risk score from individual risk factors
    /// </summary>
    private static int CalculateOverallRiskScore(List<RiskFactor> riskFactors)
    {
        if (!riskFactors.Any())
            return 0;

        // Use weighted average based on severity
        var weightedSum = riskFactors.Sum(rf => rf.RiskScore * GetSeverityWeight(rf.Severity));
        var totalWeight = riskFactors.Sum(rf => GetSeverityWeight(rf.Severity));

        return totalWeight > 0 ? (int)(weightedSum / totalWeight) : 0;
    }

    /// <summary>
    /// Gets weight factor for risk severity
    /// </summary>
    private static decimal GetSeverityWeight(string severity)
    {
        return severity switch
        {
            "Critical" or "Emergency" => 3.0m,
            "High" => 2.0m,
            "Medium" => 1.5m,
            "Low" => 1.0m,
            _ => 1.0m
        };
    }

    /// <summary>
    /// Determines risk level based on overall score
    /// </summary>
    private static string DetermineRiskLevel(int riskScore)
    {
        return riskScore switch
        {
            >= 80 => "Critical",
            >= 60 => "High", 
            >= 40 => "Medium",
            >= 20 => "Low",
            _ => "Minimal"
        };
    }

    /// <summary>
    /// Generates recommendations based on risk factors
    /// </summary>
    private static List<string> GenerateRecommendations(List<RiskFactor> riskFactors, string riskLevel)
    {
        var recommendations = new List<string>();

        if (riskLevel == "Critical")
        {
            recommendations.Add("IMMEDIATE ACTION REQUIRED: Schedule emergency inspection and maintenance");
        }

        // Group by category for specific recommendations
        var defectRisks = riskFactors.Where(rf => rf.Category == "Defect").ToList();
        var measurementRisks = riskFactors.Where(rf => rf.Category == "Measurement").ToList();
        var maintenanceRisks = riskFactors.Where(rf => rf.Category == "Maintenance").ToList();

        if (defectRisks.Any(rf => rf.Severity == "Critical"))
        {
            recommendations.Add("Address critical defects immediately");
        }

        if (measurementRisks.Any(rf => rf.Severity == "Critical"))
        {
            recommendations.Add("Investigate critical measurement readings and consider operational limits");
        }

        if (maintenanceRisks.Any())
        {
            recommendations.Add("Complete overdue maintenance work orders");
            recommendations.Add("Schedule regular inspection to prevent future issues");
        }

        if (!recommendations.Any())
        {
            recommendations.Add("Continue current monitoring and maintenance schedule");
        }

        return recommendations;
    }
}

/// <summary>
/// Data transfer object for risk assessment results
/// </summary>
public record RiskAssessmentResult
{
    public required string AssetId { get; init; }
    public DateTime AssessmentDate { get; init; }
    public int OverallRiskScore { get; init; }
    public required string RiskLevel { get; init; }
    public required List<RiskFactor> RiskFactors { get; init; }
    public required List<string> Recommendations { get; init; }
}

/// <summary>
/// Represents an individual risk factor
/// </summary>
public record RiskFactor
{
    public required string Category { get; init; }
    public required string Description { get; init; }
    public int RiskScore { get; init; }
    public required string Severity { get; init; }
    public required string Details { get; init; }
}