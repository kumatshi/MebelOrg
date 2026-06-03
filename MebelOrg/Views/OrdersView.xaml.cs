using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MebelOrg.Helpers;
using MebelOrg.Models;
using MebelOrg.Services;

namespace MebelOrg.Views;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
        AdminPanel.Visibility = SessionManager.CanManageOrders ? Visibility.Visible : Visibility.Collapsed;
    }

    public async void RefreshData() => await LoadOrdersDataAsync();

    private async Task LoadOrdersDataAsync()
    {
        try
        {
            var orders = await OrderService.GetAllAsync();
            OrdersGrid.ItemsSource = new ObservableCollection<Order>(orders);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки заказов: {ex.Message}", "Ошибка");
        }
    }

    private Order? SelectedOrder => OrdersGrid.SelectedItem as Order;

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OrderEditDialog(new Order { OrderDate = DateTime.Today, DeliveryDate = DateTime.Today.AddDays(3) });
        if (dialog.ShowDialog() == true)
            await LoadOrdersDataAsync();
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (SelectedOrder == null)
        {
            MessageBox.Show("Выберите заказ.", "Внимание");
            return;
        }

        var dialog = new OrderEditDialog(SelectedOrder);
        if (dialog.ShowDialog() == true)
            await LoadOrdersDataAsync();
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (SelectedOrder == null)
        {
            MessageBox.Show("Выберите заказ.", "Внимание");
            return;
        }

        if (MessageBox.Show($"Удалить заказ №{SelectedOrder.OrderNumber}?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await OrderService.DeleteAsync(SelectedOrder.Id);
            await LoadOrdersDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось удалить: {ex.Message}", "Ошибка");
        }
    }

    private void OnOrderSelectionChanged(object sender, SelectionChangedEventArgs e) { }
}