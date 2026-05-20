// 📁 API/Services/IYooKassaService.cs
using API.Models.YooKassa;

namespace API.Services;

/// <summary>
/// Интерфейс сервиса для работы с платёжной системой ЮKassa
/// </summary>
public interface IYooKassaService
{
    /// <summary>
    /// Создаёт новый платёж в ЮKassa
    /// </summary>
    /// <param name="request">Данные платежа</param>
    /// <param name="ct">Токен отмены</param>
    Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Получает информацию о платеже по его ID
    /// </summary>
    /// <param name="paymentId">ID платежа в ЮKassa</param>
    /// <param name="ct">Токен отмены</param>
    Task<PaymentResponse?> GetPaymentAsync(string paymentId, CancellationToken ct = default);

    /// <summary>
    /// Обрабатывает входящий вебхук от ЮKassa
    /// </summary>
    /// <param name="jsonBody">Тело вебхука в формате JSON</param>
    /// <param name="signature">Подпись вебхука из заголовка (опционально)</param>
    /// <param name="ct">Токен отмены</param>
    Task<WebhookResult> ProcessWebhookAsync(string jsonBody, string? signature = null, CancellationToken ct = default);

    /// <summary>
    /// Проверяет подпись вебхука (безопасность)
    /// </summary>
    bool VerifyWebhookSignature(string jsonBody, string signature, string webhookSecret);
}