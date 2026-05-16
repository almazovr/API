// 📁 DIPLOM.Models/Product.cs или API/Models/Product.cs

// 🔹 Добавьте этот using в начало файла:
using API.Models;
using System.ComponentModel.DataAnnotations.Schema;  // ← ВАЖНО!

public partial class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Brand { get; set; }
    public decimal Price { get; set; }
    public decimal? NewPrice { get; set; }
    public string? Image { get; set; }
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public string? Attributes { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // 🔹 🔹 🔹 ИСПРАВЛЕНО: Указываем точное имя столбца в БД
    [Column("category_name")]  // ← Маппим на snake_case в PostgreSQL
    public string? CategoryName { get; set; }


    // 🔹 Поля склада
    public int StockQuantity { get; set; } = 0;
    public int MinStockThreshold { get; set; } = 5;

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public virtual ProductCategory? Category { get; set; }
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
