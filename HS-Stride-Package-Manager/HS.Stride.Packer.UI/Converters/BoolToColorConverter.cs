// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace HS.Stride.Packer.UI.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? 
                    new SolidColorBrush(Color.FromRgb(0x15, 0x57, 0x24)) : // Green for valid
                    new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45));   // Red for invalid
            }
            return new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D)); // Gray default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}