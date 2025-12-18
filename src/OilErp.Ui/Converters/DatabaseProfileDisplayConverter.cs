using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OilErp.Bootstrap;
using OilErp.Core.Dto;

namespace OilErp.Ui.Converters;

public sealed class DatabaseProfileDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DatabaseProfile p) return string.Empty;

        return p switch
        {
            DatabaseProfile.Central => "Central",
            DatabaseProfile.PlantAnpz => "ANPZ",
            DatabaseProfile.PlantKrnpz => "KNPZ",
            _ => p.ToString()
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

