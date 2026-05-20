namespace API.Models.YooKassa;

/// <summary>
/// Результат обработки вебхука от ЮKassa
/// </summary>
public class WebhookResult
{
    public string? Event { get; set; }
    public string? PaymentId { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? OrderId { get; set; }
    public string? RawJson { get; set; } // Для отладки

    /// <summary>
    /// Был ли платёж успешным
    /// </summary>
    public bool IsSuccess =>
        string.Equals(Status, "succeeded", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Был ли платёж отменён
    /// </summary>
    public bool IsCanceled =>
        string.Equals(Status, "canceled", StringComparison.OrdinalIgnoreCase);
}