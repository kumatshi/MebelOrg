namespace MebelOrg.Models;

public class Order
{
    public int Id { get; set; }
    public int OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public int? PickupPointId { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string ClientFullName { get; set; } = string.Empty;
    public string PickupCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ItemsDescription { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = [];
}