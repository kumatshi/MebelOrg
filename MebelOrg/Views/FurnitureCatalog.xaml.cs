using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MebelOrg.Helpers;
using MebelOrg.Models;
using MebelOrg.Services;

namespace MebelOrg.Views;

public partial class FurnitureCatalog : UserControl
{
    private List<FurnitureItem> _allItems = [];

    public FurnitureCatalog()
    {
        InitializeComponent();

        // Настройка видимости панели фильтров (только менеджер и администратор)
        FilterPanel.Visibility = SessionManager.CanFilterFurniture ? Visibility.Visible : Visibility.Collapsed;

        // Настройка видимости кнопок управления (только администратор)
        AdminPanel.Visibility = SessionManager.CanManageFurniture ? Visibility.Visible : Visibility.Collapsed;

        if (SessionManager.CanFilterFurniture)
        {
            SortBox.ItemsSource = new[]
            {
                "Наименование (А-Я)",
                "Наименование (Я-А)",
                "Цена (возр.)",
                "Цена (убыв.)",
                "Скидка (возр.)",
                "Скидка (убыв.)"
            };
            SortBox.SelectedIndex = 0;
        }
    }

    public async void RefreshData() => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        try
        {
            _allItems = await FurnitureService.GetAllAsync();
            if (SessionManager.CanFilterFurniture)
                UpdateFilters();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateFilters()
    {
        var categories = _allItems.Select(p => p.Category).Distinct().OrderBy(x => x).ToList();
        categories.Insert(0, "Все");
        CategoryFilter.ItemsSource = categories;
        CategoryFilter.SelectedIndex = 0;

        var suppliers = _allItems.Select(p => p.Supplier).Distinct().OrderBy(x => x).ToList();
        suppliers.Insert(0, "Все");
        SupplierFilter.ItemsSource = suppliers;
        SupplierFilter.SelectedIndex = 0;
    }

    private void ApplyFilters()
    {
        var query = _allItems.AsEnumerable();

        if (SessionManager.CanFilterFurniture)
        {
            var search = SearchBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Article.Contains(search) ||
                    p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (CategoryFilter.SelectedItem is string cat && cat != "Все")
                query = query.Where(p => p.Category == cat);

            if (SupplierFilter.SelectedItem is string sup && sup != "Все")
                query = query.Where(p => p.Supplier == sup);

            query = SortBox.SelectedIndex switch
            {
                1 => query.OrderByDescending(p => p.Name),
                2 => query.OrderBy(p => p.Price),
                3 => query.OrderByDescending(p => p.Price),
                4 => query.OrderBy(p => p.DiscountPercent),
                5 => query.OrderByDescending(p => p.DiscountPercent),
                _ => query.OrderBy(p => p.Name)
            };
        }

        FurnitureGrid.ItemsSource = new ObservableCollection<FurnitureItem>(query.ToList());
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilters();

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        if (CategoryFilter.Items.Count > 0) CategoryFilter.SelectedIndex = 0;
        if (SupplierFilter.Items.Count > 0) SupplierFilter.SelectedIndex = 0;
        if (SortBox.Items.Count > 0) SortBox.SelectedIndex = 0;
    }

    private FurnitureItem? Selected => FurnitureGrid.SelectedItem as FurnitureItem;

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new FurnitureEditDialog(new FurnitureItem());
        if (dialog.ShowDialog() == true) await LoadDataAsync();
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("Выберите товар.", "Внимание");
            return;
        }

        var copy = new FurnitureItem
        {
            Id = Selected.Id,
            Article = Selected.Article,
            Name = Selected.Name,
            Unit = Selected.Unit,
            Price = Selected.Price,
            Supplier = Selected.Supplier,
            Manufacturer = Selected.Manufacturer,
            Category = Selected.Category,
            DiscountPercent = Selected.DiscountPercent,
            QuantityInStock = Selected.QuantityInStock,
            Description = Selected.Description,
            ImageFile = Selected.ImageFile
        };

        var dialog = new FurnitureEditDialog(copy);
        if (dialog.ShowDialog() == true) await LoadDataAsync();
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("Выберите товар.", "Внимание");
            return;
        }

        if (MessageBox.Show($"Удалить товар «{Selected.Name}»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await FurnitureService.DeleteAsync(Selected.Id);
            await LoadDataAsync();
        }
    }
}