// 📁 API/Models/YooKassa/PaymentRequest.cs
namespace API.Models.YooKassa;

public class PaymentRequest
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";
    public string Description { get; set; } = string.Empty;

    // 👇 ДОБАВЬТЕ эти свойства:
    public string? PayerEmail { get; set; }
    public string? PayerPhone { get; set; }
}