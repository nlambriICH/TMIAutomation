using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace TMIAutomation.Runner
{
    public class PlansAndPlanSumsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is IEnumerable<PlanOrPlanSum> planAndPlanSums ? string.Join(", ", planAndPlanSums.Select(p => p.Id)) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
