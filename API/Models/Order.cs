// 📁 API/Models/Order.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models;

[Table("orders")]
public partial class Order
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("cashier_id")]
    public int? CashierId { get; set; }

    [Column("status_id")]
    public int? StatusId { get; set; }

    [Column("total_amount", TypeName = "decimal(10,2)")]
    public decimal TotalAmount { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // 🔹 Навигационные свойства
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual OrderStatus? Status { get; set; }
    public virtual User? User { get; set; }
    public virtual User? Cashier { get; set; }

    // 🔹 🔹 🔹 НОВОЕ: Связь с платежом ЮKassa
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}