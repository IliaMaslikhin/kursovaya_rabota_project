namespace OilErp.Domain.ValueObjects;

/// <summary>
/// Value object representing a risk threshold configuration
/// </summary>
public record RiskThreshold
{
    public string Name { get; init; }
    public decimal LowThreshold { get; init; }
    public decimal MediumThreshold { get; init; }
    public decimal HighThreshold { get; init; }
    public decimal CriticalThreshold { get; init; }
    public string Unit { get; init; }
    public string Description { get; init; }

    public RiskThreshold(
        string name,
        decimal lowThreshold,
        decimal mediumThreshold,
        decimal highThreshold,
        decimal criticalThreshold,
        string unit,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (lowThreshold >= mediumThreshold || 
            mediumThreshold >= highThreshold || 
            highThreshold >= criticalThreshold)
        {
            throw new ArgumentException("Thresholds must be in ascending order: Low < Medium < High < Critical");
        }

        Name = name;
        LowThreshold = lowThreshold;
        MediumThreshold = mediumThreshold;
        HighThreshold = highThreshold;
        CriticalThreshold = criticalThreshold;
        Unit = unit;
        Description = description;
    }

    public string GetRiskLevel(decimal value)
    {
        return value switch
        {
            _ when value >= CriticalThreshold => "Critical",
            _ when value >= HighThreshold => "High",
            _ when value >= MediumThreshold => "Medium",
            _ when value >= LowThreshold => "Low",
            _ => "Normal"
        };
    }

    public bool ExceedsThreshold(decimal value, string thresholdLevel)
    {
        return thresholdLevel.ToLower() switch
        {
            "low" => value >= LowThreshold,
            "medium" => value >= MediumThreshold,
            "high" => value >= HighThreshold,
            "critical" => value >= CriticalThreshold,
            _ => false
        };
    }
}

/// <summary>
/// Value object representing inspection criteria
/// </summary>
public record InspectionCriteria
{
    public string InspectionType { get; init; }
    public TimeSpan Frequency { get; init; }
    public string[] RequiredMeasurements { get; init; }
    public string[] QualifiedPersonnel { get; init; }
    public string Procedure { get; init; }

    public InspectionCriteria(
        string inspectionType,
        TimeSpan frequency,
        string[] requiredMeasurements,
        string[] qualifiedPersonnel,
        string procedure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inspectionType);
        ArgumentNullException.ThrowIfNull(requiredMeasurements);
        ArgumentNullException.ThrowIfNull(qualifiedPersonnel);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedure);

        if (frequency <= TimeSpan.Zero)
            throw new ArgumentException("Frequency must be positive", nameof(frequency));

        InspectionType = inspectionType;
        Frequency = frequency;
        RequiredMeasurements = requiredMeasurements;
        QualifiedPersonnel = qualifiedPersonnel;
        Procedure = procedure;
    }

    public bool IsInspectionDue(DateTime lastInspectionDate)
    {
        return DateTime.UtcNow - lastInspectionDate >= Frequency;
    }

    public DateTime GetNextInspectionDate(DateTime lastInspectionDate)
    {
        return lastInspectionDate.Add(Frequency);
    }

    public bool IsPersonnelQualified(string personnelRole)
    {
        return QualifiedPersonnel.Contains(personnelRole, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Value object representing contact information
/// </summary>
public record ContactInfo
{
    public string Name { get; init; }
    public string Email { get; init; }
    public string Phone { get; init; }
    public string? Department { get; init; }
    public string? Role { get; init; }

    public ContactInfo(string name, string email, string phone, string? department = null, string? role = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        if (!IsValidEmail(email))
            throw new ArgumentException("Invalid email format", nameof(email));

        Name = name;
        Email = email;
        Phone = phone;
        Department = department;
        Role = role;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public string GetDisplayName()
    {
        return Role != null ? $"{Name} ({Role})" : Name;
    }
}