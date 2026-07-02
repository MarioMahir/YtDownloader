using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using YtDownloader.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace YtDownloader.Converters
{
    // ── Bool → Visibility ──────────────────────────────────────────────────────
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public Visibility TrueValue  { get; set; } = Visibility.Visible;
        public Visibility FalseValue { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? TrueValue : FalseValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == TrueValue;
    }

    // ── Null → Visibility ──────────────────────────────────────────────────────
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public Visibility NullValue    { get; set; } = Visibility.Collapsed;
        public Visibility NonNullValue { get; set; } = Visibility.Visible;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? NullValue : NonNullValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── DownloadStatus → Brush ─────────────────────────────────────────────────
    [ValueConversion(typeof(DownloadStatus), typeof(Brush))]
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DownloadStatus status) return Brushes.Gray;
            switch (status)
            {
                case DownloadStatus.Completed:
                    return new SolidColorBrush(Color.FromRgb(0x00, 0xC7, 0x8C));
                case DownloadStatus.Failed:
                    return new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
                case DownloadStatus.Cancelled:
                    return new SolidColorBrush(Color.FromRgb(0x4A, 0x4F, 0x6A));
                case DownloadStatus.Downloading:
                case DownloadStatus.Fetching:
                case DownloadStatus.Converting:
                    return new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x5F));
                default:
                    return new SolidColorBrush(Color.FromRgb(0x8B, 0x90, 0xA8));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── DownloadStatus → Visibility (solo cuando está activo) ─────────────────
    [ValueConversion(typeof(DownloadStatus), typeof(Visibility))]
    public class ActiveStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DownloadStatus status) return Visibility.Collapsed;
            return (status == DownloadStatus.Downloading
                 || status == DownloadStatus.Fetching
                 || status == DownloadStatus.Converting)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── bool invertido ─────────────────────────────────────────────────────────
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? (object)!b : false;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? (object)!b : false;
    }

    // ── string vacío → Visibility ──────────────────────────────────────────────
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class EmptyStringToVisibilityConverter : IValueConverter
    {
        public Visibility EmptyValue    { get; set; } = Visibility.Collapsed;
        public Visibility NonEmptyValue { get; set; } = Visibility.Visible;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrWhiteSpace(value as string) ? EmptyValue : NonEmptyValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── ProgressBar 0-100 Value → ScaleX (0-1) para el indicador de relleno ─────
    [ValueConversion(typeof(double), typeof(double))]
    public class ProgressToScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? Math.Clamp(d / 100.0, 0.0, 1.0) : 0.0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
