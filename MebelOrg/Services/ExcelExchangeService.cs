using ClosedXML.Excel;
using Npgsql;
using MebelOrg.Helpers;
using System.IO;

namespace MebelOrg.Services;

public static class ExcelExchangeService
{
    public const string SheetRoles = "roles";
    public const string SheetUsers = "users";
    public const string SheetPickupPoints = "pickup_points";
    public const string SheetProducts = "products";
    public const string SheetOrders = "orders";
    public const string SheetOrderItems = "order_items";

    public static async Task ExportAllAsync(string filePath, IProgress<string>? progress = null)
    {
        progress?.Report("Экспорт ролей...");
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();

        using var workbook = new XLWorkbook();

        await ExportSheetAsync(connection, workbook, SheetRoles, """
            SELECT id, name FROM roles ORDER BY id
            """, r => new object?[] { r.GetInt32(0), r.GetString(1) });

        progress?.Report("Экспорт пользователей...");
        await ExportSheetAsync(connection, workbook, SheetUsers, """
            SELECT u.id, r.name, u.full_name, u.login, u.password
            FROM users u
            JOIN roles r ON r.id = u.role_id
            ORDER BY u.id
            """, r => new object?[]
        {
            r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)
        });

        progress?.Report("Экспорт пунктов выдачи...");
        await ExportSheetAsync(connection, workbook, SheetPickupPoints, """
            SELECT id, address FROM pickup_points ORDER BY id
            """, r => new object?[] { r.GetInt32(0), r.GetString(1) });

        progress?.Report("Экспорт товаров...");
        await ExportSheetAsync(connection, workbook, SheetProducts, """
            SELECT id, article, name, unit, price, supplier, manufacturer, category,
                   discount_percent, quantity_in_stock, COALESCE(description,''), COALESCE(image_file,'')
            FROM products ORDER BY id
            """, r => new object?[]
        {
            r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDecimal(4),
            r.GetString(5), r.GetString(6), r.GetString(7), r.GetInt32(8), r.GetInt32(9),
            r.GetString(10), r.GetString(11)
        });

        progress?.Report("Экспорт заказов...");
        await ExportSheetAsync(connection, workbook, SheetOrders, """
            SELECT id, order_number, order_date, delivery_date, pickup_point_id,
                   client_full_name, COALESCE(pickup_code,''), status
            FROM orders ORDER BY order_number
            """, r => new object?[]
        {
            r.GetInt32(0), r.GetInt32(1), r.GetDateTime(2), r.GetDateTime(3),
            r.IsDBNull(4) ? null : r.GetInt32(4),
            r.GetString(5), r.GetString(6), r.GetString(7)
        });

        progress?.Report("Экспорт позиций заказов...");
        await ExportSheetAsync(connection, workbook, SheetOrderItems, """
            SELECT oi.id, o.order_number, p.article, oi.quantity
            FROM order_items oi
            JOIN orders o ON o.id = oi.order_id
            JOIN products p ON p.id = oi.product_id
            ORDER BY o.order_number, oi.id
            """, r => new object?[]
        {
            r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3)
        });

        workbook.SaveAs(filePath);
        progress?.Report("Экспорт завершён.");
    }

    public static async Task ImportAllAsync(string filePath, IProgress<string>? progress = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл не найден.", filePath);

        var imageFolder = Path.GetDirectoryName(filePath) ?? ConfigManager.DataImportFolder;

        using var workbook = new XLWorkbook(filePath);
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            progress?.Report("Очистка таблиц...");
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM order_items");
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM orders");
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM products");
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM pickup_points");
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM users");

            if (workbook.Worksheets.TryGetWorksheet(SheetRoles, out var rolesWs))
            {
                progress?.Report("Импорт ролей...");
                await ImportRolesAsync(connection, transaction, rolesWs);
            }

            if (workbook.Worksheets.TryGetWorksheet(SheetPickupPoints, out var ppWs))
            {
                progress?.Report("Импорт пунктов выдачи...");
                await ImportPickupPointsAsync(connection, transaction, ppWs);
            }

            if (workbook.Worksheets.TryGetWorksheet(SheetProducts, out var prodWs))
            {
                progress?.Report("Импорт товаров...");
                await ImportProductsAsync(connection, transaction, prodWs, imageFolder);
            }

            if (workbook.Worksheets.TryGetWorksheet(SheetUsers, out var usersWs))
            {
                progress?.Report("Импорт пользователей...");
                await ImportUsersAsync(connection, transaction, usersWs);
            }

            if (workbook.Worksheets.TryGetWorksheet(SheetOrders, out var ordersWs))
            {
                progress?.Report("Импорт заказов...");
                await ImportOrdersAsync(connection, transaction, ordersWs);
            }

            if (workbook.Worksheets.TryGetWorksheet(SheetOrderItems, out var itemsWs))
            {
                progress?.Report("Импорт позиций заказов...");
                await ImportOrderItemsAsync(connection, transaction, itemsWs);
            }

            await ResetSequencesAsync(connection, transaction);
            await transaction.CommitAsync();
            progress?.Report("Импорт завершён.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task ExportSheetAsync(NpgsqlConnection connection, XLWorkbook workbook, string sheetName, string sql, Func<NpgsqlDataReader, object?[]> mapRow)
    {
        var worksheet = workbook.Worksheets.Add(sheetName);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var rowIndex = 1;
        var colCount = reader.FieldCount;
        for (var c = 0; c < colCount; c++)
            worksheet.Cell(rowIndex, c + 1).Value = reader.GetName(c);

        while (await reader.ReadAsync())
        {
            rowIndex++;
            var values = mapRow(reader);
            for (var c = 0; c < values.Length; c++)
                SetCellValue(worksheet.Cell(rowIndex, c + 1), values[c]);
        }

        worksheet.Columns().AdjustToContents();
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = string.Empty; break;
            case DateTime dt: cell.Value = dt; break;
            case decimal d: cell.Value = d; break;
            case int i: cell.Value = i; break;
            case double dbl: cell.Value = dbl; break;
            default: cell.Value = value.ToString() ?? string.Empty; break;
        }
    }

    private static async Task ImportRolesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IXLWorksheet worksheet)
    {
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var name = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            const string sql = "INSERT INTO roles (name) VALUES (@n) ON CONFLICT (name) DO NOTHING";
            await using var cmd = new NpgsqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("n", name);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ImportPickupPointsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IXLWorksheet worksheet)
    {
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var address = GetCellString(row, 2);
            if (string.IsNullOrWhiteSpace(address)) continue;

            if (TryGetInt(row, 1, out var id) && id > 0)
            {
                const string sql = "INSERT INTO pickup_points (id, address) VALUES (@id, @a)";
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("a", address);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var cmd = new NpgsqlCommand("INSERT INTO pickup_points (address) VALUES (@a)", connection, transaction);
                cmd.Parameters.AddWithValue("a", address);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task ImportProductsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IXLWorksheet worksheet, string imageFolder)
    {
        var destFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Products");
        Directory.CreateDirectory(destFolder);

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var article = GetCellString(row, 2);
            if (string.IsNullOrWhiteSpace(article)) continue;

            var imageFile = GetCellString(row, 12);
            if (!string.IsNullOrWhiteSpace(imageFile))
            {
                var srcPath = Path.Combine(imageFolder, imageFile);
                if (File.Exists(srcPath))
                    File.Copy(srcPath, Path.Combine(destFolder, imageFile), true);
            }

            if (TryGetInt(row, 1, out var id) && id > 0)
            {
                const string sql = """
                    INSERT INTO products (id, article, name, unit, price, supplier, manufacturer, category,
                        discount_percent, quantity_in_stock, description, image_file)
                    VALUES (@id, @a, @n, @u, @p, @s, @m, @c, @d, @q, @desc, @img)
                    """;
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                AddProductParams(cmd, row, article, imageFile);
                cmd.Parameters.AddWithValue("id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                const string sql = """
                    INSERT INTO products (article, name, unit, price, supplier, manufacturer, category,
                        discount_percent, quantity_in_stock, description, image_file)
                    VALUES (@a, @n, @u, @p, @s, @m, @c, @d, @q, @desc, @img)
                    """;
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                AddProductParams(cmd, row, article, imageFile);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static void AddProductParams(NpgsqlCommand cmd, IXLRow row, string article, string imageFile)
    {
        cmd.Parameters.AddWithValue("a", article);
        cmd.Parameters.AddWithValue("n", GetCellString(row, 3));
        cmd.Parameters.AddWithValue("u", GetCellString(row, 4));
        cmd.Parameters.AddWithValue("p", GetCellDecimal(row, 5));
        cmd.Parameters.AddWithValue("s", GetCellString(row, 6));
        cmd.Parameters.AddWithValue("m", GetCellString(row, 7));
        cmd.Parameters.AddWithValue("c", GetCellString(row, 8));
        cmd.Parameters.AddWithValue("d", GetCellInt(row, 9));
        cmd.Parameters.AddWithValue("q", GetCellInt(row, 10));
        cmd.Parameters.AddWithValue("desc", GetCellString(row, 11));
        cmd.Parameters.AddWithValue("img", imageFile);
    }

    private static async Task ImportUsersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IXLWorksheet worksheet)
    {
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var login = GetCellString(row, 4);
            if (string.IsNullOrWhiteSpace(login)) continue;

            var role = GetCellString(row, 2);
            var name = GetCellString(row, 3);
            var pass = GetCellString(row, 5);

            if (TryGetInt(row, 1, out var id) && id > 0)
            {
                const string sql = """
                    INSERT INTO users (id, role_id, full_name, login, password)
                    SELECT @id, r.id, @name, @login, @pass FROM roles r WHERE r.name = @role
                    """;
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("name", name);
                cmd.Parameters.AddWithValue("login", login);
                cmd.Parameters.AddWithValue("pass", pass);
                cmd.Parameters.AddWithValue("role", role);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                const string sql = """
                    INSERT INTO users (role_id, full_name, login, password)
                    SELECT r.id, @name, @login, @pass FROM roles r WHERE r.name = @role
                    """;
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("name", name);
                cmd.Parameters.AddWithValue("login", login);
                cmd.Parameters.AddWithValue("pass", pass);
                cmd.Parameters.AddWithValue("role", role);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task ImportOrdersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IXLWorksheet worksheet)
    {
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var orderNumber = GetCellInt(row, 2);
            if (orderNumber <= 0) continue;

            var orderDate = GetCellDate(row, 3);
            var deliveryDate = GetCellDate(row, 4);
            TryGetInt(row, 5, out var pickupPointId);
            var client = GetCellString(row, 6);
            var code = GetCellString(row, 7);
            var status = GetCellString(row, 8);

            if (TryGetInt(row, 1, out var id) && id > 0)
            {
                const string sql = """
                    INSERT INTO orders (id, order_number, order_date, delivery_date, pickup_point_id,
                        client_full_name, pickup_code, status)
                    VALUES (@id, @num, @od, @dd, @pp, @client, @code, @status)
                    """;
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("num", orderNumber);
                cmd.Parameters.AddWithValue("od", orderDate);
                cmd.Parameters.AddWithValue("dd", deliveryDate);
                cmd.Parameters.AddWithValue("pp", pickupPointId > 0 ? pickupPointId : DBNull.Value);
                cmd.Parameters.AddWithValue("client", client);
                cmd.Parameters.AddWithValue("code", code);
                cmd.Parameters.AddWithValue("status", status);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                const string sql = """
                    INSERT INTO orders (order_number, order_date, delivery_date, pickup_point_id,
                        client_full_name, pickup_code, status)
                    VALUES (@num, @od, @dd, @pp, @client, @code, @status)
                    """;
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("num", orderNumber);
                cmd.Parameters.AddWithValue("od", orderDate);
                cmd.Parameters.AddWithValue("dd", deliveryDate);
                cmd.Parameters.AddWithValue("pp", pickupPointId > 0 ? pickupPointId : DBNull.Value);
                cmd.Parameters.AddWithValue("client", client);
                cmd.Parameters.AddWithValue("code", code);
                cmd.Parameters.AddWithValue("status", status);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task ImportOrderItemsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IXLWorksheet worksheet)
    {
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var orderNumber = GetCellInt(row, 2);
            var article = GetCellString(row, 3);
            var qty = GetCellInt(row, 4);
            if (orderNumber <= 0 || string.IsNullOrWhiteSpace(article) || qty <= 0) continue;

            const string sql = """
                INSERT INTO order_items (order_id, product_id, quantity)
                SELECT o.id, p.id, @q
                FROM orders o
                JOIN products p ON p.article = @a
                WHERE o.order_number = @num
                """;
            await using var cmd = new NpgsqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("q", qty);
            cmd.Parameters.AddWithValue("a", article);
            cmd.Parameters.AddWithValue("num", orderNumber);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ResetSequencesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        string[] tables = ["roles", "users", "pickup_points", "products", "orders", "order_items"];
        foreach (var table in tables)
        {
            var sql = $"""
                SELECT setval(pg_get_serial_sequence('{table}', 'id'),
                    COALESCE((SELECT MAX(id) FROM {table}), 1), true)
                """;
            await using var cmd = new NpgsqlCommand(sql, connection, transaction);
            try { await cmd.ExecuteNonQueryAsync(); }
            catch { }
        }
    }

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string GetCellString(IXLRow row, int col) => row.Cell(col).GetString().Trim();
    private static int GetCellInt(IXLRow row, int col)
    {
        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.Number)
            return (int)cell.GetDouble();
        return int.TryParse(cell.GetString(), out var v) ? v : 0;
    }

    private static decimal GetCellDecimal(IXLRow row, int col)
    {
        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.Number)
            return Convert.ToDecimal(cell.GetDouble());
        return decimal.TryParse(cell.GetString().Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static DateTime GetCellDate(IXLRow row, int col)
    {
        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().Date;
        if (cell.DataType == XLDataType.Number)
            return DateTime.FromOADate(cell.GetDouble()).Date;
        return DateTime.TryParse(cell.GetString(), out var dt) ? dt.Date : DateTime.Today;
    }

    private static bool TryGetInt(IXLRow row, int col, out int value)
    {
        value = GetCellInt(row, col);
        return value > 0;
    }
}