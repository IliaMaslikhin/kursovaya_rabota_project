namespace OilErp.Domain.ValueObjects;

/// <summary>
/// Value object representing asset dimensions
/// </summary>
public record AssetDimensions
{
    public decimal Length { get; init; }
    public decimal? Diameter { get; init; }
    public decimal? WallThickness { get; init; }
    public string Unit { get; init; } = "mm";

    public AssetDimensions(decimal length, decimal? diameter = null, decimal? wallThickness = null, string unit = "mm")
    {
        if (length <= 0)
            throw new ArgumentException("Length must be positive", nameof(length));
        
        if (diameter.HasValue && diameter <= 0)
            throw new ArgumentException("Diameter must be positive", nameof(diameter));
        
        if (wallThickness.HasValue && wallThickness <= 0)
            throw new ArgumentException("Wall thickness must be positive", nameof(wallThickness));

        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        Length = length;
        Diameter = diameter;
        WallThickness = wallThickness;
        Unit = unit;
    }

    public decimal GetVolume()
    {
        if (!Diameter.HasValue)
            throw new InvalidOperationException("Cannot calculate volume without diameter");
        
        var radius = Diameter.Value / 2;
        return (decimal)(Math.PI * (double)(radius * radius) * (double)Length);
    }

    public decimal? GetCrossSectionalArea()
    {
        if (!Diameter.HasValue)
            return null;
        
        var radius = Diameter.Value / 2;
        return (decimal)(Math.PI * (double)(radius * radius));
    }
}

/// <summary>
/// Value object representing geographic coordinates
/// </summary>
public record Coordinates
{
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public decimal? Elevation { get; init; }

    public Coordinates(decimal latitude, decimal longitude, decimal? elevation = null)
    {
        if (latitude < -90 || latitude > 90)
            throw new ArgumentException("Latitude must be between -90 and 90 degrees", nameof(latitude));
        
        if (longitude < -180 || longitude > 180)
            throw new ArgumentException("Longitude must be between -180 and 180 degrees", nameof(longitude));

        Latitude = latitude;
        Longitude = longitude;
        Elevation = elevation;
    }

    public double DistanceTo(Coordinates other)
    {
        ArgumentNullException.ThrowIfNull(other);

        const double earthRadius = 6371; // Earth's radius in kilometers
        
        var lat1Rad = (double)Latitude * Math.PI / 180;
        var lat2Rad = (double)other.Latitude * Math.PI / 180;
        var deltaLat = (double)(other.Latitude - Latitude) * Math.PI / 180;
        var deltaLon = (double)(other.Longitude - Longitude) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return earthRadius * c;
    }
}

/// <summary>
/// Value object representing measurement tolerance
/// </summary>
public record MeasurementTolerance
{
    public decimal MinValue { get; init; }
    public decimal MaxValue { get; init; }
    public decimal TargetValue { get; init; }
    public string Unit { get; init; }

    public MeasurementTolerance(decimal minValue, decimal maxValue, decimal targetValue, string unit)
    {
        if (minValue >= maxValue)
            throw new ArgumentException("Minimum value must be less than maximum value");
        
        if (targetValue < minValue || targetValue > maxValue)
            throw new ArgumentException("Target value must be within min/max range");

        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        MinValue = minValue;
        MaxValue = maxValue;
        TargetValue = targetValue;
        Unit = unit;
    }

    public bool IsWithinTolerance(decimal value)
    {
        return value >= MinValue && value <= MaxValue;
    }

    public decimal GetDeviationFromTarget(decimal value)
    {
        return Math.Abs(value - TargetValue);
    }

    public decimal GetDeviationPercentage(decimal value)
    {
        if (TargetValue == 0)
            return 0;
        
        return Math.Abs(value - TargetValue) / TargetValue * 100;
    }
}

/// <summary>
/// Value object representing an address
/// </summary>
public record Address
{
    public string Street { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string PostalCode { get; init; }
    public string Country { get; init; }

    public Address(string street, string city, string state, string postalCode, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public string GetFullAddress()
    {
        return $"{Street}, {City}, {State} {PostalCode}, {Country}";
    }
}