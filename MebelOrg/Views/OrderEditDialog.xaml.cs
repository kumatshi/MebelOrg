using System.Collections.ObjectModel;
using System.Windows;
using MebelOrg.Models;
using MebelOrg.Services;

namespace MebelOrg.Views;

public partial class OrderEditDialog : Window
{
    private readonly Order _order;
    private readonly ObservableCollection<OrderItem> _items = [];
    private List<FurnitureItem> _products = [];

    public OrderEditDialog(Order order)
    {
        InitializeComponent();
        _order = order;
        Title = order.Id == 0 ? "Заказ — добавление" : "Заказ — редактирование";
        Loaded += async (_, _) => await OnLoadedAsync();
    }

    private async Task OnLoadedAsync()
    {
        var points = await OrderService.GetPickupPointsAsync();
        PickupCombo.ItemsSource = points;
        if (_order.PickupPointId.HasValue)
            PickupCombo.SelectedItem = points.FirstOrDefault(p => p.Id == _order.PickupPointId);

        _products = await FurnitureService.GetAllAsync();
        ProductCombo.ItemsSource = _products;

        NumberInput.Text = _order.OrderNumber == 0 ? "" : _order.OrderNumber.ToString();
        OrderDatePicker.SelectedDate = _order.OrderDate;
        DeliveryDatePicker.SelectedDate = _order.DeliveryDate;
        CustomerInput.Text = _order.ClientFullName;
        CodeInput.Text = _order.PickupCode;
        StatusInput.Text = string.IsNullOrWhiteSpace(_order.Status) ? "Новый" : _order.Status;

        foreach (var item in _order.Items)
        {
            _items.Add(new OrderItem
            {
                Id = item.Id,
                OrderId = item.OrderId,
                ProductId = item.ProductId,
                Article = item.Article,
                ProductName = item.ProductName,
                Quantity = item.Quantity
            });
        }

        ItemsGrid.ItemsSource = _items;
    }

    private void OnAddItemClick(object sender, RoutedEventArgs e)
    {
        if (ProductCombo.SelectedItem is not FurnitureItem product)
            return;

        if (!int.TryParse(QuantityInput.Text, out var qty) || qty <= 0)
        {
            MessageBox.Show("Укажите количество.", "Внимание");
            return;
        }

        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null)
            existing.Quantity += qty;
        else
        {
            _items.Add(new OrderItem
            {
                ProductId = product.Id,
                Article = product.Article,
                ProductName = product.Name,
                Quantity = qty
            });
        }
    }

    private void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is OrderItem item)
            _items.Remove(item);
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(NumberInput.Text, out var number) || number <= 0)
        {
            MessageBox.Show("Укажите номер заказа.", "Ошибка");
            return;
        }

        if (OrderDatePicker.SelectedDate == null || DeliveryDatePicker.SelectedDate == null)
        {
            MessageBox.Show("Укажите даты.", "Ошибка");
            return;
        }

        if (_items.Count == 0)
        {
            MessageBox.Show("Добавьте позиции в заказ.", "Ошибка");
            return;
        }

        _order.OrderNumber = number;
        _order.OrderDate = OrderDatePicker.SelectedDate.Value;
        _order.DeliveryDate = DeliveryDatePicker.SelectedDate.Value;
        _order.PickupPointId = (PickupCombo.SelectedItem as PickupPoint)?.Id;
        _order.ClientFullName = CustomerInput.Text.Trim();
        _order.PickupCode = CodeInput.Text.Trim();
        _order.Status = StatusInput.Text.Trim();
        _order.Items = _items.ToList();

        try
        {
            await OrderService.SaveAsync(_order);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка");
        }
    }
}