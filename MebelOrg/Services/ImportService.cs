using ClosedXML.Excel;
using Npgsql;
using MebelOrg.Helpers;
using System.IO;

namespace MebelOrg.Services;

public static class ImportService
{
    public static async Task RunImportAsync(IProgress<string>? progress = null)
    {
        var folder = ConfigManager.DataImportFolder;
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Папка импорта не найдена: {folder}");

        progress?.Report("Импорт пользователей...");
        await ImportUsersAsync(Path.Combine(folder, "user_import.xlsx"));

        progress?.Report("Импорт пунктов выдачи...");
        await ImportPickupPointsAsync(folder);

        progress?.Report("Импорт товаров...");
        await ImportProductsAsync(Path.Combine(folder, "Tovar.xlsx"), folder);

        progress?.Report("Импорт заказов...");
        await ImportOrdersAsync(folder);
    }

    private static async Task ImportUsersAsync(string path)
    {
        using var workbook = new XLWorkbook(path);
        var rows = workbook.Worksheet(1).RowsUsed().Skip(1);
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();

        foreach (var row in rows)
        {
            var roleName = row.Cell(1).GetString().Trim();
            var fullName = row.Cell(2).GetString().Trim();
            var login = row.Cell(3).GetString().Trim();
            var password = row.Cell(4).GetString().Trim();
            if (string.IsNullOrWhiteSpace(login)) continue;

            const string sql = """
                INSERT INTO users (role_id, full_name, login, password)
                SELECT r.id, @name, @login, @pass FROM roles r WHERE r.name = @role
                ON CONFLICT (login) DO UPDATE SET full_name=@name, password=@pass, role_id=(SELECT id FROM roles WHERE name=@role)
                """;
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("name", fullName);
            cmd.Parameters.AddWithValue("login", login);
            cmd.Parameters.AddWithValue("pass", password);
            cmd.Parameters.AddWithValue("role", roleName);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ImportPickupPointsAsync(string folder)
    {
        var file = Directory.GetFiles(folder, "*выдачи*.xlsx").FirstOrDefault()
                   ?? Directory.GetFiles(folder, "*.xlsx")
                       .FirstOrDefault(f => !f.Contains("Tovar") && !f.Contains("user") && !f.Contains("заказ"));

        if (file == null) return;

        using var workbook = new XLWorkbook(file);
        var rows = workbook.Worksheet(1).RowsUsed();
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var clearCmd = new NpgsqlCommand("DELETE FROM pickup_points", connection);
        await clearCmd.ExecuteNonQueryAsync();

        foreach (var row in rows)
        {
            var address = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(address)) continue;
            await using var cmd = new NpgsqlCommand("INSERT INTO pickup_points (address) VALUES (@a)", connection);
            cmd.Parameters.AddWithValue("a", address);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ImportProductsAsync(string path, string imageFolder)
    {
        using var workbook = new XLWorkbook(path);
        var rows = workbook.Worksheet(1).RowsUsed().Skip(1);
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var clearCmd = new NpgsqlCommand("DELETE FROM order_items; DELETE FROM products", connection);
        await clearCmd.ExecuteNonQueryAsync();
        var destFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Products");
        Directory.CreateDirectory(destFolder);

        foreach (var row in rows)
        {
            var article = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(article)) continue;

            var imageFile = row.Cell(11).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(imageFile))
            {
                var srcPath = Path.Combine(imageFolder, imageFile);
                if (File.Exists(srcPath))
                    File.Copy(srcPath, Path.Combine(destFolder, imageFile), true);
            }

            const string sql = """
                INSERT INTO products (article, name, unit, price, supplier, manufacturer, category,
                    discount_percent, quantity_in_stock, description, image_file)
                VALUES (@a, @n, @u, @p, @s, @m, @c, @d, @q, @desc, @img)
                """;
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("a", article);
            cmd.Parameters.AddWithValue("n", row.Cell(2).GetString().Trim());
            cmd.Parameters.AddWithValue("u", row.Cell(3).GetString().Trim());
            cmd.Parameters.AddWithValue("p", Convert.ToDecimal(row.Cell(4).GetDouble()));
            cmd.Parameters.AddWithValue("s", row.Cell(5).GetString().Trim());
            cmd.Parameters.AddWithValue("m", row.Cell(6).GetString().Trim());
            cmd.Parameters.AddWithValue("c", row.Cell(7).GetString().Trim());
            cmd.Parameters.AddWithValue("d", (int)row.Cell(8).GetDouble());
            cmd.Parameters.AddWithValue("q", (int)row.Cell(9).GetDouble());
            cmd.Parameters.AddWithValue("desc", row.Cell(10).GetString().Trim());
            cmd.Parameters.AddWithValue("img", imageFile);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ImportOrdersAsync(string folder)
    {
        var file = Directory.GetFiles(folder, "*заказ*.xlsx").FirstOrDefault()
                   ?? Directory.GetFiles(folder, "*.xlsx")
                       .FirstOrDefault(f => f.Contains("import") && !f.Contains("user") && !f.Contains("Tovar") && !f.Contains("выдачи"));

        if (file == null) return;

        using var workbook = new XLWorkbook(file);
        var rows = workbook.Worksheet(1).RowsUsed().Skip(1);
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var clearCmd = new NpgsqlCommand("DELETE FROM order_items; DELETE FROM orders", connection);
        await clearCmd.ExecuteNonQueryAsync();

        foreach (var row in rows)
        {
            if (!int.TryParse(row.Cell(1).GetString(), out var orderNumber) || orderNumber <= 0)
                continue;

            var itemsRaw = row.Cell(2).GetString();
            var orderDate = ParseExcelDate(row.Cell(3));
            var deliveryDate = ParseExcelDate(row.Cell(4));
            var pickupPointId = (int)row.Cell(5).GetDouble();
            var client = row.Cell(6).GetString().Trim();
            var pickupCode = row.Cell(7).GetString().Trim();
            var status = row.Cell(8).GetString().Trim();

            const string insertOrder = """
                INSERT INTO orders (order_number, order_date, delivery_date, pickup_point_id,
                    client_full_name, pickup_code, status)
                VALUES (@num, @od, @dd, @pp, @client, @code, @status)
                RETURNING id
                """;
            await using var cmd = new NpgsqlCommand(insertOrder, connection);
            cmd.Parameters.AddWithValue("num", orderNumber);
            cmd.Parameters.AddWithValue("od", orderDate);
            cmd.Parameters.AddWithValue("dd", deliveryDate);
            cmd.Parameters.AddWithValue("pp", pickupPointId);
            cmd.Parameters.AddWithValue("client", client);
            cmd.Parameters.AddWithValue("code", pickupCode);
            cmd.Parameters.AddWithValue("status", status);
            var orderId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            await InsertOrderItemsAsync(connection, orderId, itemsRaw);
        }
    }

    private static async Task InsertOrderItemsAsync(NpgsqlConnection connection, int orderId, string itemsRaw)
    {
        var parts = itemsRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var article = parts[i];
            if (!int.TryParse(parts[i + 1], out var qty)) continue;

            await using var findCmd = new NpgsqlCommand("SELECT id FROM products WHERE article=@a", connection);
            findCmd.Parameters.AddWithValue("a", article);
            var productId = await findCmd.ExecuteScalarAsync();
            if (productId == null) continue;

            await using var insertCmd = new NpgsqlCommand(
                "INSERT INTO order_items (order_id, product_id, quantity) VALUES (@o, @p, @q)", connection);
            insertCmd.Parameters.AddWithValue("o", orderId);
            insertCmd.Parameters.AddWithValue("p", Convert.ToInt32(productId));
            insertCmd.Parameters.AddWithValue("q", qty);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private static DateTime ParseExcelDate(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().Date;
        if (double.TryParse(cell.GetString(), out var serial))
            return DateTime.FromOADate(serial).Date;
        if (DateTime.TryParse(cell.GetString(), out var dt))
            return dt.Date;
        return DateTime.Today;
    }
}