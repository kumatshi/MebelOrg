using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MebelOrg.Models;

namespace MebelOrg.Converters;

public class RowColorConverter : IValueConverter
{
    // Фон для товаров со скидкой > 15% - #008080
    public static readonly Brush HighDiscountBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#008080")!);
    public static readonly Brush NormalBrush = Brushes.White;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FurnitureItem item && item.HasHighDiscount)
            return HighDiscountBrush;
        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}