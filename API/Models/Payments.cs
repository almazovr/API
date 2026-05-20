// 📁 API/Models/Payment.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("order_id")]
    [Required]
    public int OrderId { get; set; }

    [Column("yookassa_payment_id")]
    [StringLength(100)]
    public string YooKassaPaymentId { get; set; } = string.Empty;

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = "pending"; // pending, succeeded, canceled, waiting_for_capture

    public decimal Amount { get; set; }

    [Column("currency")]
    [StringLength(3)]
    public string Currency { get; set; } = "RUB";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // 🔹 Навигационное свойство
    public virtual Order Order { get; set; } = null!;
}