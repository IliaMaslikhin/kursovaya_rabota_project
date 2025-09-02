using FluentAssertions;
using OilErp.Domain.Entities;
using OilErp.Domain.Services;
using OilErp.Domain.ValueObjects;

namespace OilErp.Tests;

public class AssetTests
{
    [Fact]
    public void Asset_Creation_Should_Set_Properties_Correctly()
    {
        // Arrange
        var assetId = "ASSET_001";
        var tagNumber = "PL-001-P01";
        var description = "Main Pipeline Section 1";
        var plantCode = "PLT001";
        var assetType = "Pipeline";

        // Act
        var asset = new Asset
        {
            Id = assetId,
            TagNumber = tagNumber,
            Description = description,
            PlantCode = plantCode,
            AssetType = assetType
        };

        // Assert
        asset.Id.Should().Be(assetId);
        asset.TagNumber.Should().Be(tagNumber);
        asset.Description.Should().Be(description);
        asset.PlantCode.Should().Be(plantCode);
        asset.AssetType.Should().Be(assetType);
        asset.Segments.Should().BeEmpty();
        asset.Defects.Should().BeEmpty();
        asset.WorkOrders.Should().BeEmpty();
    }

    [Fact]
    public void Asset_AddSegment_Should_Add_Segment_To_Collection()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "ASSET_001",
            TagNumber = "PL-001",
            PlantCode = "PLT001"
        };

        var segment = new Segment
        {
            AssetId = asset.Id,
            SegmentName = "Section A",
            LengthM = 100.0m
        };

        // Act
        asset.AddSegment(segment);

        // Assert
        asset.Segments.Should().HaveCount(1);
        asset.Segments.First().Should().Be(segment);
        asset.GetTotalLength().Should().Be(100.0m);
    }

    [Fact]
    public void Asset_AddSegment_Should_Throw_When_Segment_Belongs_To_Different_Asset()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "ASSET_001",
            TagNumber = "PL-001",
            PlantCode = "PLT001"
        };

        var segment = new Segment
        {
            AssetId = "DIFFERENT_ASSET",
            SegmentName = "Section A",
            LengthM = 100.0m
        };

        // Act & Assert
        var action = () => asset.AddSegment(segment);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Segment must belong to this asset");
    }

    [Fact]
    public void Asset_HasCriticalDefects_Should_Return_True_When_Critical_Defect_Exists()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "ASSET_001",
            TagNumber = "PL-001",
            PlantCode = "PLT001"
        };

        var criticalDefect = new Defect
        {
            AssetId = asset.Id,
            DefectType = "Crack",
            Severity = "Critical",
            DiscoveredAt = DateTime.UtcNow
        };

        asset.AddDefect(criticalDefect);

        // Act & Assert
        asset.HasCriticalDefects().Should().BeTrue();
    }

    [Fact]
    public void Asset_HasCriticalDefects_Should_Return_False_When_No_Critical_Defects()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "ASSET_001",
            TagNumber = "PL-001",
            PlantCode = "PLT001"
        };

        var mediumDefect = new Defect
        {
            AssetId = asset.Id,
            DefectType = "Corrosion",
            Severity = "Medium",
            DiscoveredAt = DateTime.UtcNow
        };

        asset.AddDefect(mediumDefect);

        // Act & Assert
        asset.HasCriticalDefects().Should().BeFalse();
    }
}

public class MeasurementPointTests
{
    [Fact]
    public void MeasurementPoint_AddReading_Should_Add_Reading_To_Collection()
    {
        // Arrange
        var measurementPoint = new MeasurementPoint
        {
            SegmentId = Guid.NewGuid(),
            PointName = "MP-001",
            MeasurementType = "WallThickness"
        };

        var reading = new Reading
        {
            PointId = measurementPoint.Id,
            Value = 8.5m,
            Unit = "mm",
            MeasuredAt = DateTime.UtcNow
        };

        // Act
        measurementPoint.AddReading(reading);

        // Assert
        measurementPoint.Readings.Should().HaveCount(1);
        measurementPoint.GetLatestReading().Should().Be(reading);
    }

    [Fact]
    public void MeasurementPoint_GetAverageReading_Should_Calculate_Correctly()
    {
        // Arrange
        var measurementPoint = new MeasurementPoint
        {
            SegmentId = Guid.NewGuid(),
            PointName = "MP-001",
            MeasurementType = "WallThickness"
        };

        var readings = new[]
        {
            new Reading { PointId = measurementPoint.Id, Value = 8.0m, Unit = "mm", MeasuredAt = DateTime.UtcNow },
            new Reading { PointId = measurementPoint.Id, Value = 8.5m, Unit = "mm", MeasuredAt = DateTime.UtcNow },
            new Reading { PointId = measurementPoint.Id, Value = 9.0m, Unit = "mm", MeasuredAt = DateTime.UtcNow }
        };

        foreach (var reading in readings)
        {
            measurementPoint.AddReading(reading);
        }

        // Act
        var average = measurementPoint.GetAverageReading();

        // Assert
        average.Should().Be(8.5m);
    }
}

public class ValueObjectTests
{
    [Fact]
    public void AssetDimensions_Should_Calculate_Volume_Correctly()
    {
        // Arrange
        var dimensions = new AssetDimensions(length: 100m, diameter: 10m, wallThickness: 1m);

        // Act
        var volume = dimensions.GetVolume();

        // Assert
        volume.Should().BeApproximately(7853.98m, 0.01m); // π * 5² * 100
    }

    [Fact]
    public void RiskThreshold_Should_Determine_Risk_Level_Correctly()
    {
        // Arrange
        var threshold = new RiskThreshold(
            "Wall Thickness",
            lowThreshold: 1.0m,
            mediumThreshold: 2.0m,
            highThreshold: 4.0m,
            criticalThreshold: 6.0m,
            "mm",
            "Minimum wall thickness requirements"
        );

        // Act & Assert
        threshold.GetRiskLevel(8.0m).Should().Be("Critical");
        threshold.GetRiskLevel(5.0m).Should().Be("High");
        threshold.GetRiskLevel(3.0m).Should().Be("Medium");
        threshold.GetRiskLevel(1.5m).Should().Be("Low");
        threshold.GetRiskLevel(0.5m).Should().Be("Normal");
    }

    [Fact]
    public void Coordinates_Should_Calculate_Distance_Correctly()
    {
        // Arrange
        var coord1 = new Coordinates(latitude: 0m, longitude: 0m);
        var coord2 = new Coordinates(latitude: 1m, longitude: 1m);

        // Act
        var distance = coord1.DistanceTo(coord2);

        // Assert
        distance.Should().BeGreaterThan(0);
        distance.Should().BeLessThan(200); // Should be around 157 km
    }
}