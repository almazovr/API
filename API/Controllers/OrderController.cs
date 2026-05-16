// 📁 API/Controllers/OrderController.cs
using API.DTOs;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly DiplomContext _context;

    private const int STATUS_PREPARING = 8;
    private const int STATUS_READY = 9;
    private const int STATUS_CANCELLED = 10;
    private const int STATUS_ISSUED = 11;

    public OrderController(DiplomContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> Get() =>
        await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Status)
            .Include(o => o.Cashier)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                UserId = o.UserId,
                UserName = o.User != null ? o.User.Login : null,
                CashierId = o.CashierId,
                CashierName = o.Cashier != null ? o.Cashier.Login : null,
                StatusId = o.StatusId,
                StatusName = o.Status != null ? o.Status.Name : null,
                TotalAmount = o.TotalAmount,
                Comment = o.Comment,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetByUserId(int userId)
    {
        var orders = await _context.Orders
            .Include(o => o.Status)
            .Include(o => o.OrderItems)
            .Include(o => o.Cashier)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                UserId = o.UserId,
                UserName = o.User != null ? o.User.Login : null,
                CashierId = o.CashierId,
                CashierName = o.Cashier != null ? o.Cashier.Login : null,
                StatusId = o.StatusId,
                StatusName = o.Status != null ? o.Status.Name : null,
                TotalAmount = o.TotalAmount,
                Comment = o.Comment,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDetailDto>> Get(int id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Status)
            .Include(o => o.OrderItems)
            .Include(o => o.Cashier)
            .Where(o => o.Id == id)
            .Select(o => new OrderDetailDto
            {
                Id = o.Id,
                UserId = o.UserId,
                UserName = o.User != null ? o.User.Login : null,
                CashierId = o.CashierId,
                CashierName = o.Cashier != null ? o.Cashier.Login : null,
                StatusId = o.StatusId,
                StatusName = o.Status != null ? o.Status.Name : null,
                TotalAmount = o.TotalAmount,
                Comment = o.Comment,
                CreatedAt = o.CreatedAt,
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    OrderId = oi.OrderId,
                    ProductId = oi.ProductId,
                    ProductName = oi.ProductName,
                    ProductBrand = oi.ProductBrand,
                    Quantity = oi.Quantity,
                    PriceAtTime = oi.PriceAtTime,
                    CreatedAt = oi.CreatedAt
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Post([FromBody] OrderCreateDto dto)
    {
        if (dto.UserId <= 0)
            return BadRequest(new { message = "UserId обязателен" });

        if (dto.StatusId.HasValue &&
            dto.StatusId.Value != STATUS_PREPARING &&
            dto.StatusId.Value != STATUS_READY &&
            dto.StatusId.Value != STATUS_CANCELLED &&
            dto.StatusId.Value != STATUS_ISSUED)
        {
            return BadRequest(new { message = $"Недопустимый статус." });
        }

        var cartItems = await _context.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.Cart.UserId == dto.UserId)
            .ToListAsync();

        if (!cartItems.Any())
            return BadRequest(new { message = "Корзина пуста" });

        foreach (var ci in cartItems)
        {
            if (ci.Product != null && ci.Product.StockQuantity < ci.Quantity)
            {
                return BadRequest(new
                {
                    message = $"Недостаточно товара \"{ci.Product.Name}\" на складе."
                });
            }
        }

        var order = new Order
        {
            UserId = dto.UserId,
            StatusId = dto.StatusId.HasValue ? dto.StatusId.Value : STATUS_PREPARING,
            TotalAmount = cartItems.Sum(ci => (ci.Product?.Price ?? 0) * ci.Quantity),
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        foreach (var ci in cartItems)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = ci.ProductId,
                ProductName = ci.Product != null ? ci.Product.Name : "Товар удалён",
                ProductBrand = ci.Product != null ? ci.Product.Brand : null,
                Quantity = ci.Quantity,
                PriceAtTime = ci.Product != null ? ci.Product.Price : 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.OrderItems.Add(orderItem);

            if (ci.Product != null)
            {
                ci.Product.StockQuantity -= ci.Quantity;
                var stockMovement = new StockMovement
                {
                    ProductId = ci.ProductId,
                    UserId = dto.UserId,
                    QuantityChange = -ci.Quantity,
                    MovementType = "order",
                    Comment = $"Заказ #{order.Id}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.StockMovements.Add(stockMovement);
            }
        }

        _context.CartItems.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        var result = await _context.Orders
            .Include(o => o.Status)
            .Include(o => o.Cashier)
            .Where(o => o.Id == order.Id)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                UserId = o.UserId,
                StatusId = o.StatusId,
                StatusName = o.Status != null ? o.Status.Name : null,
                CashierId = o.CashierId,
                CashierName = o.Cashier != null ? o.Cashier.Login : null,
                TotalAmount = o.TotalAmount,
                Comment = o.Comment,
                CreatedAt = o.CreatedAt
            })
            .FirstOrDefaultAsync();

        return CreatedAtAction(nameof(Get), new { id = order.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] OrderUpdateDto dto)
    {
        if (id != dto.Id) return BadRequest();
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        order.StatusId = dto.StatusId;
        order.Comment = dto.Comment;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] OrderStatusUpdateDto dto)
    {
        Debug.WriteLine($"🔄 API: Обновление статуса заказа #{id} → {dto.StatusId}, CashierId={dto.CashierId}");

        if (dto.StatusId != STATUS_PREPARING && dto.StatusId != STATUS_READY &&
            dto.StatusId != STATUS_CANCELLED && dto.StatusId != STATUS_ISSUED)
        {
            return BadRequest(new { message = $"Недопустимый статус." });
        }

        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound(new { message = "Заказ не найден" });
        if (order.StatusId == STATUS_ISSUED)
            return BadRequest(new { message = "Нельзя изменить статус уже выданного заказа" });

        order.StatusId = dto.StatusId;
        order.UpdatedAt = DateTime.UtcNow;

        if (dto.StatusId == STATUS_ISSUED && dto.CashierId.HasValue)
        {
            var cashier = await _context.Users.FindAsync(dto.CashierId.Value);
            if (cashier != null && cashier.RoleId == 7)
            {
                order.CashierId = dto.CashierId.Value;
                Debug.WriteLine($"✅ API: Кассир #{dto.CashierId.Value} назначен для заказа #{id}");
            }
        }

        try
        {
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        _context.OrderItems.RemoveRange(order.OrderItems);
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }
    [HttpGet("report/cashier")]
    public async Task<ActionResult<IEnumerable<CashierReportDto>>> GetCashierReport(
        [FromQuery] int managerId,
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] int statusId)
    {
        Debug.WriteLine($"📊 API: Запрос отчёта по кассирам. ManagerId={managerId}, " +
                       $"Period=[{dateFrom:yyyy-MM-dd}]—[{dateTo:yyyy-MM-dd}], StatusId={statusId}");

        try
        {
            // 🔹 Конвертируем локальные даты в UTC
            var dateFromUtc = dateFrom.Date.ToUniversalTime();
            var dateToUtc = dateTo.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            Debug.WriteLine($"🔍 UTC-фильтр: [{dateFromUtc:yyyy-MM-dd HH:mm}] — [{dateToUtc:yyyy-MM-dd HH:mm}]");

            // 🔹 🔹 🔹 ИСПРАВЛЕНО: фильтруем null CreatedAt и используем .Value.Date
            var report = await _context.Orders
                .Where(o => o.StatusId == statusId
                         && o.CashierId.HasValue
                         && o.CreatedAt.HasValue  // 🔹 Исключаем заказы без даты
                         && o.CreatedAt.Value >= dateFromUtc
                         && o.CreatedAt.Value <= dateToUtc)
                .Include(o => o.Cashier)
                .GroupBy(o => new
                {
                    o.CashierId,
                    // 🔹 🔹 🔹 ИСПРАВЛЕНО: используем .Value.Date для nullable DateTime
                    WorkDate = o.CreatedAt!.Value.Date
                })
                .Select(g => new CashierReportDto
                {
                    UserId = g.Key.CashierId.Value,
                    CashierName = g.First().Cashier != null
                        ? g.First().Cashier.Login
                        : $"Кассир #{g.Key.CashierId}",
                    WorkDate = g.Key.WorkDate,
                    OrdersCount = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(r => r.WorkDate)
                .ThenBy(r => r.CashierName)
                .ToListAsync();

            Debug.WriteLine($"✅ API: Найдено {report.Count} строк в отчёте");

            foreach (var row in report.Take(3))
            {
                Debug.WriteLine($"   📋 {row.WorkDate:dd.MM}: {row.CashierName} — {row.OrdersCount} заказов, {row.TotalAmount:C0}");
            }

            return Ok(report);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"💥 API: Ошибка GetCashierReport: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
        }
    }

    [HttpGet("report/cashier/export")]
    public async Task<IActionResult> GetCashierReportCsv(
        [FromQuery] int managerId,
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] int statusId)
    {
        Debug.WriteLine($"💾 API: Экспорт CSV отчёта. ManagerId={managerId}, " +
                       $"Period=[{dateFrom:yyyy-MM-dd}]—[{dateTo:yyyy-MM-dd}], StatusId={statusId}");

        try
        {
            var dateFromUtc = dateFrom.Date.ToUniversalTime();
            var dateToUtc = dateTo.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            var rawData = await _context.Orders
                .Where(o => o.StatusId == statusId
                         && o.CashierId.HasValue
                         && o.CreatedAt.HasValue
                         && o.CreatedAt.Value >= dateFromUtc
                         && o.CreatedAt.Value <= dateToUtc)
                .Include(o => o.Cashier)
                .GroupBy(o => new
                {
                    o.CashierId,
                    WorkDate = o.CreatedAt!.Value.Date
                })
                .Select(g => new
                {
                    CashierId = g.Key.CashierId.Value,
                    CashierLogin = g.First().Cashier != null ? g.First().Cashier.Login : null,
                    WorkDate = g.Key.WorkDate,
                    OrdersCount = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync();

            var report = rawData.Select(r => new CashierReportDto
            {
                UserId = r.CashierId,
                CashierName = r.CashierLogin ?? $"Кассир #{r.CashierId}",
                WorkDate = r.WorkDate,
                OrdersCount = r.OrdersCount,
                TotalAmount = r.TotalAmount
            })
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.CashierName)
            .ToList();

            // 🔹 Формируем CSV с разделителем ТОЧКА С ЗАПЯТОЙ
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Дата;Кассир;Заказов;Сумма (₽)");

            decimal grandTotal = 0;
            int grandCount = 0;

            foreach (var row in report)
            {
                csv.AppendLine($"{row.WorkDate:dd.MM.yyyy};{row.CashierName};{row.OrdersCount};{row.TotalAmount:F2}");
                grandTotal += row.TotalAmount;
                grandCount += row.OrdersCount;
            }

            csv.AppendLine($"ИТОГО;;{grandCount};{grandTotal:F2}");

            // 🔹 🔹 🔹 ИСПОЛЬЗУЕМ WINDOWS-1251 (кириллица) вместо UTF-8!
            // Excel на русском Windows понимает эту кодировку нативно
            var win1251 = System.Text.Encoding.GetEncoding(1251);
            var bytes = win1251.GetBytes(csv.ToString());

            var fileName = $"cashier_report_{dateFrom:yyyy-MM-dd}_to_{dateTo:yyyy-MM-dd}.csv";

            Debug.WriteLine($"✅ API: Файл готов: {fileName} ({bytes.Length} байт, Windows-1251)");

            // 🔹 Возвращаем файл
            return File(bytes, "text/csv; charset=windows-1251", fileName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"💥 API: Ошибка GetCashierReportCsv: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
        }
    }


}

// ============================================================================
// 🔹 DTO для OrderController (определены в этом же файле)
// ============================================================================

public class OrderDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public int? CashierId { get; set; }
    public string? CashierName { get; set; }
    public int? StatusId { get; set; }
    public string? StatusName { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<OrderItemDto>? OrderItems { get; set; }
}

public class OrderDetailDto : OrderDto
{
    public new List<OrderItemDto>? Items { get; set; }
}

public class OrderCreateDto
{
    public int UserId { get; set; }
    public int? StatusId { get; set; }
    public string? Comment { get; set; }
}

public class OrderUpdateDto
{
    public int Id { get; set; }
    public int StatusId { get; set; }
    public string? Comment { get; set; }
}

public class OrderStatusUpdateDto
{
    public int StatusId { get; set; }
    public int? CashierId { get; set; }
}

public class OrderAdminDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? CashierId { get; set; }
    public string? CashierName { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }
}

