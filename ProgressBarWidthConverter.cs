using System;
using System.Globalization;
using System.Windows.Data;

namespace GWChanger
{
    public class ProgressBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 ||
                !(values[0] is double value) ||
                !(values[1] is double minimum) ||
                !(values[2] is double maximum) ||
                !(values[3] is double actualWidth))
            {
                return 0.0;
            }

            if (maximum - minimum == 0)
                return 0.0;

            // Calculate width based on percentage
            double percentage = (value - minimum) / (maximum - minimum);
            return percentage * actualWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}