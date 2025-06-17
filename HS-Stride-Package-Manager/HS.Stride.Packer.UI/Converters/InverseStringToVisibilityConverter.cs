// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HS.Stride.Packer.UI.Converters
{
    public class InverseStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}