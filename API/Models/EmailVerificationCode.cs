// 📁 API/Models/EmailVerificationCode.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models;

[Table("email_verification_codes")]
public class EmailVerificationCode
{
    [Key]
    [Column("id")]  // 🔹 Маппинг на колонку в БД
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    [Column("email")]
    public string Email { get; set; } = null!;

    [Required]
    [Column("code")]
    public string Code { get; set; } = null!;  // 6-значный код

    [Required]
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }  // Время истечения

    [Column("is_used")]
    public bool IsUsed { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}