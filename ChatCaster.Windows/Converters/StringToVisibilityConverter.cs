using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChatCaster.Windows.Converters
{
    /// <summary>
    /// Конвертер для преобразования строки в Visibility.
    /// Пустая или null строка → Collapsed, непустая → Visible
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StringToVisibilityConverter does not support ConvertBack");
        }
    }
    
    /// <summary>
    /// Обратный конвертер: пустая строка → Visible, непустая → Collapsed
    /// Полезно для placeholder'ов или loading состояний
    /// </summary>
    public class InverseStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("InverseStringToVisibilityConverter does not support ConvertBack");
        }
    }
    
    /// <summary>
    /// Boolean to Visibility конвертер (бонус)
    /// true → Visible, false → Collapsed
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolean)
            {
                return boolean ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            
            return false;
        }
    }
}