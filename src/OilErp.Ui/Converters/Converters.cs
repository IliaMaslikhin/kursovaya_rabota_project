using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OilErp.Core.Dto;

namespace OilErp.Ui.Converters;

public sealed class BoolInvertConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }
}

public sealed class DatabaseProfileDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DatabaseProfile.Central => "Центральная",
            DatabaseProfile.PlantAnpz => "АНПЗ",
            DatabaseProfile.PlantKrnpz => "КНПЗ",
            _ => "Неизвестно"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
