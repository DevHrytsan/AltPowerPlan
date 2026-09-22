using AltPowerPlan.Models;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace AltPowerPlan.Helpers
{
    internal class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not String enumString)
            {
                throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
            }

            if (!Enum.IsDefined(typeof(ThemeChoice), value))
            {
                throw new ArgumentException("ExceptionEnumToBooleanConverterValueMustBeAnEnum");
            }

            var enumValue = Enum.Parse(typeof(ThemeChoice), enumString);

            return enumValue.Equals(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not String enumString)
            {
                throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
            }

            return Enum.Parse(typeof(ThemeChoice), enumString);
        }
    }
}
