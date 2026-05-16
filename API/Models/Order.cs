// 📁 API/Models/Order.cs
using API.Models;

public partial class Order
{
    public int Id { get; set; }
    public int? UserId { get; set; }        // 🔹 Клиент, оформивший заказ
    public int? CashierId { get; set; }     // 🔹 🔹 🔹 НОВОЕ: Кассир, обработавший заказ
    public int? StatusId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual OrderStatus? Status { get; set; }
    public virtual User? User { get; set; }              // 🔹 Клиент
    public virtual User? Cashier { get; set; }           // 🔹 🔹 🔹 НОВОЕ: Кассир
}