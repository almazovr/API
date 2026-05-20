// 📁 API/Controllers/PaymentsController.cs
using API.Models;
using API.Models.YooKassa;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;              // ← 🔹 ДОБАВЛЕНО: для StreamReader
using System.Security;
using System.Text;

namespace API.Controllers;

/// <summary>
/// Контроллер для обработки платежей через ЮKassa
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IYooKassaService _yooKassaService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly DiplomContext _context;

    public PaymentsController(
        IYooKassaService yooKassaService,
        ILogger<PaymentsController> logger,
        DiplomContext context)
    {
        _yooKassaService = yooKassaService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Создаёт новый платёж в ЮKassa
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [AllowAnonymous]
    public async Task<IActionResult> CreatePayment(
        [FromBody] PaymentRequest request,
        CancellationToken ct)
    {
        if (request.Amount <= 0)
            return BadRequest(new { error = "Сумма платежа должна быть больше нуля" });

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return BadRequest(new { error = "OrderId обязателен" });

        try
        {
            _logger.LogInformation("Запрос на создание платежа: {OrderId}", request.OrderId);

            var response = await _yooKassaService.CreatePaymentAsync(request, ct);

            // 🔹 🔹 🔹 СОХРАНЯЕМ СВЯЗЬ ORDER <-> PAYMENT В БД
            if (int.TryParse(request.OrderId, out int orderId))
            {
                // 🔹 Проверка: не создаём дубликат, если платёж уже сохранён
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.YooKassaPaymentId == response.PaymentId, ct);

                if (existingPayment == null)
                {
                    var payment = new Payment
                    {
                        OrderId = orderId,
                        YooKassaPaymentId = response.PaymentId,
                        Status = response.Status,
                        Amount = request.Amount,
                        Currency = request.Currency,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync(ct);

                    _logger?.LogInformation($"💾 Сохранён платёж: OrderId={orderId}, YooKassaId={response.PaymentId}");
                }
                else
                {
                    _logger?.LogWarning($"⚠️ Платёж {response.PaymentId} уже сохранён для заказа #{orderId}");
                }
            }

            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ошибка связи с ЮKassa при создании платежа {OrderId}", request.OrderId);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Ошибка связи с платёжным шлюзом" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Ошибка валидации при создании платежа {OrderId}", request.OrderId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непредвиденная ошибка при создании платежа {OrderId}", request.OrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получает информацию о платеже по ID
    /// </summary>
    [HttpGet("{paymentId}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    public async Task<IActionResult> GetPayment(string paymentId, CancellationToken ct)
    {
        var payment = await _yooKassaService.GetPaymentAsync(paymentId, ct);

        return payment is not null
            ? Ok(payment)
            : NotFound(new { error = "Платёж не найден" });
    }

    /// <summary>
    /// Обрабатывает вебхуки от ЮKassa
    /// 🔹 АВТОМАТИЧЕСКИ обновляет статус заказа и платежа в БД
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {


        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);

            // 🔹 ДОБАВЬТЕ ЭТУ СТРОКУ для отладки:
            _logger.LogInformation($"📥 Webhook received: {body}");

            var signature = Request.Headers["HTTP_YOOKASSA_SIGNATURE"].FirstOrDefault()
                         ?? Request.Headers["X-YooKassa-Signature"].FirstOrDefault();

            var result = await _yooKassaService.ProcessWebhookAsync(body, signature, ct);

            // 🔹 Логика обновления статуса в БД
            if (result.IsSuccess && !string.IsNullOrEmpty(result.OrderId))
            {
                _logger.LogInformation("✅ WEBHOOK: Заказ {OrderId} оплачен. Сумма: {Amount} {Currency}",
                    result.OrderId, result.Amount, result.Currency);

                if (int.TryParse(result.OrderId, out int orderId))
                {
                    // 🔹 1. Находим заказ
                    var order = await _context.Orders.FindAsync(orderId);

                    // 🔹 2. Находим платёж по PaymentId из вебхука
                    var payment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.YooKassaPaymentId == result.PaymentId, ct);

                    if (payment != null)
                    {
                        // 🔹 Обновляем статус платежа
                        if (payment.Status != result.Status)
                        {
                            payment.Status = result.Status;
                            payment.UpdatedAt = DateTime.UtcNow;
                            _logger.LogDebug($"🔄 Payment #{payment.Id} status updated: {result.Status}");
                        }
                    }
                    else if (order != null)
                    {
                        // 🔹 Если платежа нет в БД, но есть заказ — создаём запись
                        // (на случай, если платёж был создан вручную или через другой канал)
                        var newPayment = new Payment
                        {
                            OrderId = orderId,
                            YooKassaPaymentId = result.PaymentId ?? string.Empty,
                            Status = result.Status ?? "pending",
                            Amount = result.Amount,
                            Currency = result.Currency ?? "RUB",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Payments.Add(newPayment);
                        _logger.LogDebug($"💾 Created new Payment record for order #{orderId}");
                    }

                    // 🔹 3. Если заказ ещё не оплачен — обновляем его статус
                    const int STATUS_PAID = 11; // "Выдан/Оплачен"

                    if (order != null && order.StatusId != STATUS_PAID)
                    {
                        order.StatusId = STATUS_PAID;
                        order.UpdatedAt = DateTime.UtcNow;

                        await _context.SaveChangesAsync(ct);

                        _logger.LogInformation($"✅ DB: Заказ #{orderId} и платёж обновлены. Статус: {STATUS_PAID}");
                    }
                    else if (order == null)
                    {
                        _logger.LogWarning($"⚠️ DB: Заказ #{orderId} не найден в базе данных");
                    }
                    else
                    {
                        _logger.LogDebug($"ℹ️ DB: Заказ #{orderId} уже имеет статус {STATUS_PAID}");
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠️ WEBHOOK: Не удалось распарсить OrderId '{result.OrderId}' в число");
                }
            }
            else if (result.IsCanceled)
            {
                _logger.LogWarning("❌ WEBHOOK: Платёж {PaymentId} отменён", result.PaymentId);

                // 🔹 Опционально: обновить статус платежа на "canceled"
                if (!string.IsNullOrEmpty(result.PaymentId))
                {
                    var payment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.YooKassaPaymentId == result.PaymentId, ct);

                    if (payment != null && payment.Status != "canceled")
                    {
                        payment.Status = "canceled";
                        payment.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(ct);
                    }
                }
            }
            else
            {
                _logger.LogDebug("ℹ️ WEBHOOK: Событие {Event}, Статус {Status}", result.Event, result.Status);
            }

            return Ok();
        }
        catch (SecurityException ex)
        {
            _logger.LogWarning(ex, "🔒 Попытка подделки вебхука");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Ошибка обработки вебхука");
            // Всегда возвращаем 200, чтобы ЮKassa не спамила повторами
            return Ok();
        }
    }
}