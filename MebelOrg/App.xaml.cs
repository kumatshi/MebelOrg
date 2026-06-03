using System.Windows;
using MebelOrg.Helpers;
using MebelOrg.Services;
using MebelOrg.Views;

namespace MebelOrg;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigManager.LoadConfiguration();

        try
        {
            await DatabaseHelper.InitializeDatabaseAsync();

            if (!await DatabaseHelper.IsDatabasePopulatedAsync())
            {
                var result = MessageBox.Show(
                    "База данных пуста. Выполнить импорт данных из папки «импорт»?",
                    "Импорт данных",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    await ImportService.RunImportAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось подключиться к PostgreSQL.\n\n{ex.Message}\n\n" +
                "Проверьте подключение к базе данных.",
                "Ошибка БД",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        var login = new AuthWindow();
        login.Show();
    }
}