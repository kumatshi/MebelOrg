using System.Windows;
using MebelOrg.Helpers;
using MebelOrg.Services;

namespace MebelOrg.Views;

public partial class AuthWindow : Window
{
    public AuthWindow()
    {
        InitializeComponent();
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        ErrorMessage.Visibility = Visibility.Collapsed;
        var login = LoginInput.Text.Trim();
        var password = PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Введите логин и пароль.");
            return;
        }

        try
        {
            var user = await AuthService.AuthenticateAsync(login, password);
            if (user == null)
            {
                ShowError("Неверный логин или пароль.");
                return;
            }

            SessionManager.CurrentUser = user;
            var main = new MainWindow(false);
            main.Show();
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка подключения: {ex.Message}");
        }
    }

    private void OnGuestClick(object sender, RoutedEventArgs e)
    {
        SessionManager.CurrentUser = null;
        var main = new MainWindow(true);
        main.Show();
        Close();
    }

    private void ShowError(string message)
    {
        ErrorMessage.Text = message;
        ErrorMessage.Visibility = Visibility.Visible;
    }
}