namespace OilErp.Domain.Entities;

/// <summary>
/// Represents a material catalog item
/// </summary>
public class Material
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal? Density { get; set; }
    public string? Type { get; set; }
    public string? Specification { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Business methods
    public void UpdateDensity(decimal density)
    {
        if (density <= 0)
            throw new ArgumentException("Density must be positive", nameof(density));
        
        Density = density;
    }

    public void SetType(string type)
    {
        Type = type;
    }

    public void SetSpecification(string specification)
    {
        Specification = specification;
    }
}

/// <summary>
/// Represents a coating catalog item
/// </summary>
public class Coating
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Type { get; set; }
    public string? Manufacturer { get; set; }
    public string? Specification { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Business methods
    public void SetType(string type)
    {
        Type = type;
    }

    public void SetManufacturer(string manufacturer)
    {
        Manufacturer = manufacturer;
    }

    public void SetSpecification(string specification)
    {
        Specification = specification;
    }
}

/// <summary>
/// Represents a fluid catalog item
/// </summary>
public class Fluid
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Corrosivity { get; set; }
    public decimal? Density { get; set; }
    public decimal? Viscosity { get; set; }
    public string? PressureRating { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Business methods
    public void SetCorrosivity(string corrosivity)
    {
        Corrosivity = corrosivity;
    }

    public void SetDensity(decimal density)
    {
        if (density <= 0)
            throw new ArgumentException("Density must be positive", nameof(density));
        
        Density = density;
    }

    public void SetViscosity(decimal viscosity)
    {
        if (viscosity <= 0)
            throw new ArgumentException("Viscosity must be positive", nameof(viscosity));
        
        Viscosity = viscosity;
    }

    public void SetPressureRating(string pressureRating)
    {
        PressureRating = pressureRating;
    }

    public bool IsCorrosive()
    {
        return !string.IsNullOrEmpty(Corrosivity) && 
               !Corrosivity.Equals("None", StringComparison.OrdinalIgnoreCase);
    }
}