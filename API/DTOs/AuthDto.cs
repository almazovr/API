using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace API.DTOs;

public class ValidLoginAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value as string))
            return new ValidationResult("Логин не может быть пустым");

        var login = (value as string)!.Trim();

        if (login.Length < 3 || login.Length > 50)
            return new ValidationResult("Логин должен быть от 3 до 50 символов");

        if (!Regex.IsMatch(login, @"^[a-zA-Z0-9_\-\.]+$"))
            return new ValidationResult("Логин может содержать только латинские буквы, цифры, _, -, .");

        if (Regex.IsMatch(login, @"^\d+$"))
            return new ValidationResult("Логин не может состоять только из цифр");

        if (login.StartsWith(".") || login.EndsWith(".") || login.Contains(".."))
            return new ValidationResult("Логин не может начинаться/заканчиваться на точку");

        return ValidationResult.Success;
    }
}

public class StrongPasswordAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 8;

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value as string))
            return new ValidationResult("Пароль не может быть пустым");

        var password = value as string;

        if (password!.Length < MinLength)
            return new ValidationResult($"Пароль должен быть не менее {MinLength} символов");

        if (password.Length > 100)
            return new ValidationResult("Пароль не может быть длиннее 100 символов");

        if (!Regex.IsMatch(password, @"[A-Z]"))
            return new ValidationResult("Пароль должен содержать заглавную букву");

        if (!Regex.IsMatch(password, @"[a-z]"))
            return new ValidationResult("Пароль должен содержать строчную букву");

        if (!Regex.IsMatch(password, @"\d"))
            return new ValidationResult("Пароль должен содержать цифру");

        if (Regex.IsMatch(password, @"[\s]"))
            return new ValidationResult("Пароль не может содержать пробелы");

        return ValidationResult.Success;
    }
}

public class RegisterRequestDto
{
    [ValidLogin]
    public string Login { get; set; } = null!;

    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Неверный формат email")]
    [StringLength(254, ErrorMessage = "Email слишком длинный")]
    public string Email { get; set; } = null!;
}

public class VerifyRegisterDto
{
    [ValidLogin]
    public string Login { get; set; } = null!;

    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Неверный формат email")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Пароль обязателен")]
    [StrongPassword]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Код обязателен")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Код должен содержать 6 цифр")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Код должен содержать 6 цифр")]
    public string Code { get; set; } = null!;
}

public class RegisterResponseDto
{
    public int Id { get; set; }
    public string Login { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? RoleId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class LoginDto
{
    [Required(ErrorMessage = "Логин или email обязателен")]
    [StringLength(100, MinimumLength = 3)]
    public string LoginOrEmail { get; set; } = null!;

    [Required(ErrorMessage = "Пароль обязателен")]
    public string Password { get; set; } = null!;

    [StringLength(100)]
    public string? DeviceInfo { get; set; }
}