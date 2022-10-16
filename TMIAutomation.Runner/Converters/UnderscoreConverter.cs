using System;
using System.Globalization;
using System.Windows.Data;

namespace TMIAutomation.Runner
{
    // This converter duplicates all underscores
    // because WPF doesn't display single underscores
    public class UnderscoreConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string s ? s.Replace("_", "__") : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}