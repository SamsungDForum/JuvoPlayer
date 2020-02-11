using System;
using Xamarin.Forms;

namespace XamarinPlayer.Tizen.TV.ViewModels
{
    public class CueVisibilityConverter: IValueConverter
    {
        public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (string.IsNullOrEmpty((string)value))
            {
                return false;
            }

            return true;
        }
        public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}