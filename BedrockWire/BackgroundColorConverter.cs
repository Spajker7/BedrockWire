using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire
{
    public class BackgroundColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasError = (bool)value;
            
            if(hasError)
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
