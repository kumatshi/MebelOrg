using Npgsql;
using MebelOrg.Models;

namespace MebelOrg.Services;

public static class FurnitureService
{
    public static async Task<List<FurnitureItem>> GetAllAsync()
    {
        const string sql = """
            SELECT id, article, name, unit, price, supplier, manufacturer, category,
                   discount_percent, quantity_in_stock, COALESCE(description, ''), COALESCE(image_file, '')
            FROM products
            ORDER BY name
            """;

        var items = new List<FurnitureItem>();
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(MapToFurniture(reader));
        return items;
    }

    public static async Task SaveAsync(FurnitureItem item)
    {
        if (item.Id == 0)
        {
            const string insertSql = """
                INSERT INTO products (article, name, unit, price, supplier, manufacturer, category,
                    discount_percent, quantity_in_stock, description, image_file)
                VALUES (@article, @name, @unit, @price, @supplier, @manufacturer, @category,
                    @discount, @qty, @description, @image)
                """;
            await ExecuteNonQueryAsync(insertSql, item);
        }
        else
        {
            const string updateSql = """
                UPDATE products SET article=@article, name=@name, unit=@unit, price=@price,
                    supplier=@supplier, manufacturer=@manufacturer, category=@category,
                    discount_percent=@discount, quantity_in_stock=@qty,
                    description=@description, image_file=@image
                WHERE id=@id
                """;
            await ExecuteNonQueryAsync(updateSql, item);
        }
    }

    public static async Task DeleteAsync(int id)
    {
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM products WHERE id=@id", connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteNonQueryAsync(string sql, FurnitureItem item)
    {
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("article", item.Article);
        command.Parameters.AddWithValue("name", item.Name);
        command.Parameters.AddWithValue("unit", item.Unit);
        command.Parameters.AddWithValue("price", item.Price);
        command.Parameters.AddWithValue("supplier", item.Supplier);
        command.Parameters.AddWithValue("manufacturer", item.Manufacturer);
        command.Parameters.AddWithValue("category", item.Category);
        command.Parameters.AddWithValue("discount", item.DiscountPercent);
        command.Parameters.AddWithValue("qty", item.QuantityInStock);
        command.Parameters.AddWithValue("description", item.Description);
        command.Parameters.AddWithValue("image", item.ImageFile);
        if (item.Id > 0)
            command.Parameters.AddWithValue("id", item.Id);
        await command.ExecuteNonQueryAsync();
    }

    private static FurnitureItem MapToFurniture(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Article = reader.GetString(1),
        Name = reader.GetString(2),
        Unit = reader.GetString(3),
        Price = reader.GetDecimal(4),
        Supplier = reader.GetString(5),
        Manufacturer = reader.GetString(6),
        Category = reader.GetString(7),
        DiscountPercent = reader.GetInt32(8),
        QuantityInStock = reader.GetInt32(9),
        Description = reader.GetString(10),
        ImageFile = reader.GetString(11)
    };
}