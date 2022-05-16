using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Text;

namespace BedrockWire.Converters
{
    public class HexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string hex = BitConverter.ToString((byte[])value).Replace("-", " ");
            StringBuilder sb = new StringBuilder(hex);
            for (int i = 95; i < hex.Length; i+= 96)
            {
                sb[i] = '\n';
            }
            return sb.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
