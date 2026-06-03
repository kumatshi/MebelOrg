using System.Windows;
using Microsoft.Win32;
using MebelOrg.Helpers;
using MebelOrg.Models;
using MebelOrg.Services;

namespace MebelOrg.Views;

public partial class MainWindow : Window
{
    private readonly bool _guestMode;
    private FurnitureCatalog? _furnitureCatalog;
    private OrdersView? _ordersView;

    public MainWindow(bool guestMode)
    {
        InitializeComponent();
        _guestMode = guestMode;

        if (_guestMode)
        {
            Title = "ООО «МебельОрг» — Товары (гость)";
            UserInfoText.Text = "Роль: Гость";
            OrdersNavBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            var user = SessionManager.CurrentUser!;
            UserInfoText.Text = $"{user.FullName} | Роль: {user.RoleName}";
            OrdersNavBtn.Visibility = SessionManager.CanViewOrders ? Visibility.Visible : Visibility.Collapsed;
            Title = $"ООО «МебельОрг» — {user.RoleName}";

            if (SessionManager.Role == UserRoleType.Admin)
            {
                ExportExcelBtn.Visibility = Visibility.Visible;
                ImportExcelBtn.Visibility = Visibility.Visible;
            }
        }

        ShowFurniture();
    }

    private async void OnExportExcelClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Экспорт в Excel",
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"MebelOrg_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            ExportExcelBtn.IsEnabled = false;
            ImportExcelBtn.IsEnabled = false;
            await ExcelExchangeService.ExportAllAsync(dialog.FileName);
            MessageBox.Show("Экспорт завершён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ExportExcelBtn.IsEnabled = true;
            ImportExcelBtn.IsEnabled = true;
        }
    }

    private async void OnImportExcelClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Импорт из Excel",
            Filter = "Excel (*.xlsx)|*.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        if (MessageBox.Show("Импорт заменит все данные. Продолжить?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            ExportExcelBtn.IsEnabled = false;
            ImportExcelBtn.IsEnabled = false;
            await ExcelExchangeService.ImportAllAsync(dialog.FileName);
            MessageBox.Show("Импорт завершён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshCurrentView();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ExportExcelBtn.IsEnabled = true;
            ImportExcelBtn.IsEnabled = true;
        }
    }

    private void RefreshCurrentView()
    {
        if (MainContent.Content is FurnitureCatalog fc) fc.RefreshData();
        else if (MainContent.Content is OrdersView ov) ov.RefreshData();
    }

    private void ShowFurniture()
    {
        _furnitureCatalog ??= new FurnitureCatalog();
        MainContent.Content = _furnitureCatalog;
        _furnitureCatalog.RefreshData();
    }

    private void ShowOrders()
    {
        if (!SessionManager.CanViewOrders) return;
        _ordersView ??= new OrdersView();
        MainContent.Content = _ordersView;
        _ordersView.RefreshData();
    }

    private void OnProductsNavClick(object sender, RoutedEventArgs e) => ShowFurniture();
    private void OnOrdersNavClick(object sender, RoutedEventArgs e) => ShowOrders();

    private void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        SessionManager.CurrentUser = null;
        new AuthWindow().Show();
        Close();
    }
}