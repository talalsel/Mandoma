using System.ComponentModel.DataAnnotations;

namespace Inventory.Web.Models;

public enum UserRole { Admin, Sales, StockOnly }

public class UserAccount
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required, StringLength(255)] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Sales;
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Sku { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(50)] public string? Category { get; set; }
    public decimal Cost { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int MinQuantity { get; set; } = 5;
}

public class Sale
{
    public int Id { get; set; }
    public DateTime SoldAt { get; set; } = DateTime.Now;
    public decimal Total { get; set; }
    public int UserId { get; set; }
    public UserAccount? User { get; set; }
    public List<SaleItem> Items { get; set; } = [];
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int QuantityChange { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int UserId { get; set; }
}
