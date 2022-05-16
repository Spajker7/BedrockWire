using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace BedrockWire.Converters
{
    public class BackgroundColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if(value != null)
            {
                return new SolidColorBrush(Color.Parse("#E6534E"));
            }

            return new SolidColorBrush(Color.Parse("#FFF"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
