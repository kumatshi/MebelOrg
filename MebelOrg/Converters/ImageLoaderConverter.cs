using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MebelOrg.Converters;

public class ImageLoaderConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var fileName = value as string;
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Поиск изображения в папке Resources/Products
        var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Products", fileName);

        if (!File.Exists(fullPath))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}