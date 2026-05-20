namespace API.Models.YooKassa;

/// <summary>
/// Ответ после создания платежа в ЮKassa
/// </summary>
public class PaymentResponse
{
    /// <summary>
    /// ID платежа в системе ЮKassa
    /// </summary>
    public string PaymentId { get; set; } = string.Empty;

    /// <summary>
    /// Ссылка на страницу оплаты (для редиректа)
    /// </summary>
    public string ConfirmationUrl { get; set; } = string.Empty;

    /// <summary>
    /// Токен для встроенной оплаты (для мобильных приложений)
    /// </summary>
    public string? ConfirmationToken { get; set; }

    /// <summary>
    /// Текущий статус платежа
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Время истечения платежа
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}