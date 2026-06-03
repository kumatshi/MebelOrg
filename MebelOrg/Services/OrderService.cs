using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using MebelOrg.Models;

namespace MebelOrg.Services;

public static class OrderService
{
    public static async Task<List<Order>> GetAllAsync()
    {
        const string sql = """
            SELECT o.id, o.order_number, o.order_date, o.delivery_date,
                   COALESCE(pp.address, ''), o.client_full_name, COALESCE(o.pickup_code, ''), o.status,
                   o.pickup_point_id
            FROM orders o
            LEFT JOIN pickup_points pp ON pp.id = o.pickup_point_id
            ORDER BY o.order_number
            """;

        var orders = new List<Order>();
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            orders.Add(new Order
            {
                Id = reader.GetInt32(0),
                OrderNumber = reader.GetInt32(1),
                OrderDate = reader.GetDateTime(2),
                DeliveryDate = reader.GetDateTime(3),
                PickupAddress = reader.GetString(4),
                ClientFullName = reader.GetString(5),
                PickupCode = reader.GetString(6),
                Status = reader.GetString(7),
                PickupPointId = reader.IsDBNull(8) ? null : reader.GetInt32(8)
            });
        }

        foreach (var order in orders)
            order.Items = await GetItemsAsync(order.Id);

        foreach (var order in orders)
            order.ItemsDescription = string.Join(", ", order.Items.Select(i => $"{i.Article} x{i.Quantity}"));

        return orders;
    }

    public static async Task<List<PickupPoint>> GetPickupPointsAsync()
    {
        var points = new List<PickupPoint>();
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id, address FROM pickup_points ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            points.Add(new PickupPoint { Id = reader.GetInt32(0), Address = reader.GetString(1) });
        return points;
    }

    public static async Task SaveAsync(Order order)
    {
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            if (order.Id == 0)
            {
                const string insertSql = """
                    INSERT INTO orders (order_number, order_date, delivery_date, pickup_point_id,
                        client_full_name, pickup_code, status)
                    VALUES (@num, @od, @dd, @pp, @client, @code, @status)
                    RETURNING id
                    """;
                await using var cmd = new NpgsqlCommand(insertSql, connection, transaction);
                AddOrderParameters(cmd, order);
                order.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            else
            {
                const string updateSql = """
                    UPDATE orders SET order_number=@num, order_date=@od, delivery_date=@dd,
                        pickup_point_id=@pp, client_full_name=@client, pickup_code=@code, status=@status
                    WHERE id=@id
                    """;
                await using var cmd = new NpgsqlCommand(updateSql, connection, transaction);
                cmd.Parameters.AddWithValue("id", order.Id);
                AddOrderParameters(cmd, order);
                await cmd.ExecuteNonQueryAsync();

                await using var delCmd = new NpgsqlCommand("DELETE FROM order_items WHERE order_id=@id", connection, transaction);
                delCmd.Parameters.AddWithValue("id", order.Id);
                await delCmd.ExecuteNonQueryAsync();
            }

            foreach (var item in order.Items)
            {
                await using var itemCmd = new NpgsqlCommand(
                    "INSERT INTO order_items (order_id, product_id, quantity) VALUES (@oid, @pid, @qty)",
                    connection, transaction);
                itemCmd.Parameters.AddWithValue("oid", order.Id);
                itemCmd.Parameters.AddWithValue("pid", item.ProductId);
                itemCmd.Parameters.AddWithValue("qty", item.Quantity);
                await itemCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public static async Task DeleteAsync(int id)
    {
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM orders WHERE id=@id", connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddOrderParameters(NpgsqlCommand command, Order order)
    {
        command.Parameters.AddWithValue("num", order.OrderNumber);
        command.Parameters.AddWithValue("od", order.OrderDate.Date);
        command.Parameters.AddWithValue("dd", order.DeliveryDate.Date);
        command.Parameters.AddWithValue("pp", (object?)order.PickupPointId ?? DBNull.Value);
        command.Parameters.AddWithValue("client", order.ClientFullName);
        command.Parameters.AddWithValue("code", order.PickupCode);
        command.Parameters.AddWithValue("status", order.Status);
    }

    private static async Task<List<OrderItem>> GetItemsAsync(int orderId)
    {
        const string sql = """
            SELECT oi.id, oi.product_id, p.article, p.name, oi.quantity
            FROM order_items oi
            JOIN products p ON p.id = oi.product_id
            WHERE oi.order_id = @id
            """;

        var items = new List<OrderItem>();
        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", orderId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new OrderItem
            {
                Id = reader.GetInt32(0),
                OrderId = orderId,
                ProductId = reader.GetInt32(1),
                Article = reader.GetString(2),
                ProductName = reader.GetString(3),
                Quantity = reader.GetInt32(4)
            });
        }
        return items;
    }
}