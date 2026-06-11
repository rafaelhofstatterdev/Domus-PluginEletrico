using System;
using System.Globalization;
using System.Windows.Data;

namespace DmEletrico.UI.Converters
{
    /// <summary>
    /// Conversor de double ↔ string que aceita ponto OU vírgula como separador
    /// decimal (ex.: "2.85" e "2,85"), independentemente da cultura do Windows.
    /// </summary>
    public sealed class FlexibleDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? d.ToString(CultureInfo.CurrentCulture) : value?.ToString() ?? "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value as string ?? "").Trim().Replace(',', '.');
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            return Binding.DoNothing;
        }
    }
}
