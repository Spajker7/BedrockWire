using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BedrockWire.Converters
{
    public class MsTimeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            ulong time = (ulong) value;
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddMilliseconds(time);
            string a = dt.ToLongTimeString() + "." + dt.Millisecond;
            return a;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
