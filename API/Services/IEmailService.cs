// 📁 API/Services/IEmailService.cs
namespace API.Services;

/// <summary>
/// Интерфейс сервиса отправки писем
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Отправка кода подтверждения на email
    /// </summary>
    /// <param name="email">Email получателя</param>
    /// <param name="code">6-значный код подтверждения</param>
    /// <param name="login">Логин пользователя (для персонализации)</param>
    Task SendVerificationCodeAsync(string email, string code, string login);
}