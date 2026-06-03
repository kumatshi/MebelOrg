namespace MebelOrg.Models;

public class FurnitureItem
{
    public int Id { get; set; }
    public string Article { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int DiscountPercent { get; set; }
    public int QuantityInStock { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ImageFile { get; set; } = string.Empty;

    public decimal FinalPrice => Price * (100 - DiscountPercent) / 100m;
    public bool HasHighDiscount => DiscountPercent > 15;
}