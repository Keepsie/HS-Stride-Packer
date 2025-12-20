// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HS.Stride.Packer.UI.Converters
{
    /// <summary>
    /// Converts a count (int) to Visibility.
    /// Returns Visible if count > 0, otherwise Collapsed.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverse of CountToVisibilityConverter.
    /// Returns Visible if count == 0, otherwise Collapsed.
    /// </summary>
    public class InverseCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
