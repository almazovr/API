// 📁 API/DTOs/UserLoginDto.cs
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

public class UserLoginDto
{
    [Required(ErrorMessage = "Логин обязателен")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Логин должен быть от 3 до 50 символов")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\.]+$", ErrorMessage = "Логин может содержать только буквы, цифры, _, -, .")]
    public string Login { get; set; } = null!;

    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть не менее 6 символов")]
    public string Password { get; set; } = null!;
}

// 📁 API/DTOs/ProductCreateDto.cs
public class ProductCreateDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [Range(0.01, 1000000, ErrorMessage = "Цена должна быть от 0.01 до 1 000 000")]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    // 🔹 Дополнительная защита от подозрительных символов
    [RegularExpression(@"^[^<>;""'\\]*$", ErrorMessage = "Недопустимые символы в названии")]
    public bool IsValidName => !Regex.IsMatch(Name ?? "", @"[<>;""'\\]");
}