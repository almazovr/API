// 📁 API/Services/EmailService.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Diagnostics;
using System.Text;

namespace API.Services;

/// <summary>
/// Реализация отправки писем через SMTP
/// 🔹 Для Gmail требуется "Пароль приложения"
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _config;

    public EmailService(ILogger<EmailService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task SendVerificationCodeAsync(string email, string code, string login)
    {
        try
        {
            // 🔹 Получаем настройки из appsettings.json
            var smtpSettings = _config.GetSection("SmtpSettings");
            var host = smtpSettings["Host"];
            var port = int.Parse(smtpSettings["Port"]);
            var username = smtpSettings["Username"];
            var password = smtpSettings["Password"];  // ← Должен быть БЕЗ дефисов!
            var fromEmail = smtpSettings["FromEmail"];
            var fromName = smtpSettings["FromName"];

            _logger.LogInformation("📧 Отправка: {From} → {To} через {Host}:{Port}",
                fromEmail, email, host, port);

            // 🔹 Создаём письмо
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(login, email));
            message.Subject = "🔐 Подтверждение регистрации в Кафе";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $"""
                <html>
                <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                    <div style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;">
                        <h1 style="color: white; margin: 0;">☕ Кафе</h1>
                        <p style="color: white; margin: 10px 0 0;">Подтверждение регистрации</p>
                    </div>
                    <div style="background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;">
                        <p>Здравствуйте, <strong>{login}</strong>!</p>
                        <p>Ваш код подтверждения:</p>
                        <div style="background: #667eea; color: white; font-size: 32px; font-weight: bold; 
                                    padding: 15px; text-align: center; border-radius: 8px; margin: 20px 0; 
                                    letter-spacing: 8px;">{code}</div>
                        <p style="color: #666;">⏰ Код действует <strong>10 минут</strong></p>
                        <p style="color: #666; font-size: 14px;">
                            Если вы не регистрировались — просто проигнорируйте это письмо.
                        </p>
                    </div>
                </body>
                </html>
                """,
                TextBody = $"Здравствуйте, {login}!\n\nВаш код подтверждения: {code}\n\nКод действует 10 минут."
            };

            message.Body = bodyBuilder.ToMessageBody();

            // 🔹 Отправка через SMTP
            using var client = new SmtpClient();

            // 🔹 Подключение
            _logger.LogDebug("🔌 Подключение к {Host}:{Port}...", host, port);
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);

            // 🔹 Аутентификация
            _logger.LogDebug("🔐 Аутентификация...");
            await client.AuthenticateAsync(username, password);

            // 🔹 Отправка
            _logger.LogDebug("📤 Отправка...");
            await client.SendAsync(message);

            await client.DisconnectAsync(true);

            _logger.LogInformation("✅ Email успешно отправлен на {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка отправки на {Email}: {Message}", email, ex.Message);

            // 🔹 Выводим код в консоль для ручного ввода (режим разработки)
            Debug.WriteLine($"""
            ⚠️ ОТПРАВКА НЕ УДАЛАСЬ — код для ручного ввода:
            👤 Логин: {login}
            ✉️ Email: {email}
            🔐 Код: {code}
            ❌ Ошибка: {ex.Message}
            """);
        }
    }
}