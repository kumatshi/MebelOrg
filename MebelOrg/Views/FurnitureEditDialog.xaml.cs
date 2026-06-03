using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MebelOrg.Models;
using MebelOrg.Services;

namespace MebelOrg.Views;

public partial class FurnitureEditDialog : Window
{
    private readonly FurnitureItem _item;

    public FurnitureEditDialog(FurnitureItem item)
    {
        InitializeComponent();
        _item = item;
        Title = item.Id == 0 ? "Добавление товара" : "Редактирование товара";

        // Заполнение полей
        ArticleInput.Text = item.Article;
        NameInput.Text = item.Name;
        UnitInput.Text = item.Unit;
        PriceInput.Text = item.Price.ToString(CultureInfo.InvariantCulture);
        SupplierInput.Text = item.Supplier;
        ManufacturerInput.Text = item.Manufacturer;
        CategoryInput.Text = item.Category;
        DiscountInput.Text = item.DiscountPercent.ToString();
        StockInput.Text = item.QuantityInStock.ToString();
        DescriptionInput.Text = item.Description;
        ImageInput.Text = item.ImageFile;

        // Загрузка предпросмотра изображения
        LoadImagePreview(item.ImageFile);

        // Проверка скидки при загрузке
        CheckDiscount();
    }

    private void LoadImagePreview(string imageFileName)
    {
        if (string.IsNullOrWhiteSpace(imageFileName))
        {
            NoImageText.Visibility = Visibility.Visible;
            PreviewImage.Visibility = Visibility.Collapsed;
            return;
        }

        var imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Products", imageFileName);

        if (File.Exists(imagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath);
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
                NoImageText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                NoImageText.Visibility = Visibility.Visible;
                PreviewImage.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            NoImageText.Visibility = Visibility.Visible;
            PreviewImage.Visibility = Visibility.Collapsed;
        }
    }

    private void DiscountInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CheckDiscount();
    }

    private void CheckDiscount()
    {
        if (int.TryParse(DiscountInput.Text, out int discount))
        {
            // Если скидка превышает 15%, показываем предупреждение
            if (discount > 15)
            {
                DiscountWarning.Visibility = Visibility.Visible;
                // Подсвечиваем поле скидки
                DiscountInput.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#008080")!);
                DiscountInput.Foreground = Brushes.White;
            }
            else
            {
                DiscountWarning.Visibility = Visibility.Collapsed;
                DiscountInput.Background = Brushes.White;
                DiscountInput.Foreground = Brushes.Black;
            }
        }
        else
        {
            DiscountWarning.Visibility = Visibility.Collapsed;
            DiscountInput.Background = Brushes.White;
            DiscountInput.Foreground = Brushes.Black;
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Проверка числовых полей
        if (!decimal.TryParse(PriceInput.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
        {
            MessageBox.Show("Проверьте поле Цена. Введите корректное число.", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
            PriceInput.Focus();
            return;
        }

        if (!int.TryParse(DiscountInput.Text, out var discount))
        {
            MessageBox.Show("Проверьте поле Скидка %. Введите целое число.", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
            DiscountInput.Focus();
            return;
        }

        if (!int.TryParse(StockInput.Text, out var stock))
        {
            MessageBox.Show("Проверьте поле Количество на складе. Введите целое число.", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StockInput.Focus();
            return;
        }

        // Проверка обязательных полей
        if (string.IsNullOrWhiteSpace(ArticleInput.Text))
        {
            MessageBox.Show("Введите артикул.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            ArticleInput.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(NameInput.Text))
        {
            MessageBox.Show("Введите наименование товара.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            NameInput.Focus();
            return;
        }

        // Заполнение объекта
        _item.Article = ArticleInput.Text.Trim();
        _item.Name = NameInput.Text.Trim();
        _item.Unit = UnitInput.Text.Trim();
        _item.Price = price;
        _item.Supplier = SupplierInput.Text.Trim();
        _item.Manufacturer = ManufacturerInput.Text.Trim();
        _item.Category = CategoryInput.Text.Trim();
        _item.DiscountPercent = discount;
        _item.QuantityInStock = stock;
        _item.Description = DescriptionInput.Text.Trim();
        _item.ImageFile = ImageInput.Text.Trim();

        try
        {
            await FurnitureService.SaveAsync(_item);

            // Показываем сообщение об успехе
            MessageBox.Show("Товар успешно сохранён!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}