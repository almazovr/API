// 📁 API/Controllers/StockController.cs
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
// 🔹 УБРАНО: [Authorize] — теперь доступно без токена
public class StockController : ControllerBase
{
    private readonly DiplomContext _context;

    public StockController(DiplomContext context) => _context = context;

    // 🔹 POST: api/stock/add — добавить товар на склад (приход)
    [HttpPost("add")]
    // 🔹 УБРАНО: [Authorize(Roles = "Admin,Warehouse")]
    public async Task<IActionResult> AddStock([FromBody] StockOperationDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return NotFound("Товар не найден");

        if (dto.Quantity <= 0)
            return BadRequest("Количество должно быть положительным");

        // 🔹 Увеличиваем остаток
        product.StockQuantity += dto.Quantity;
        product.UpdatedAt = DateTime.UtcNow;

        // 🔹 Записываем в историю
        var movement = new StockMovement
        {
            ProductId = dto.ProductId,
            UserId = dto.UserId,
            QuantityChange = dto.Quantity,
            MovementType = "in",
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };
        _context.StockMovements.Add(movement);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"✅ Добавлено {dto.Quantity} шт. «{product.Name}»",
            newStockQuantity = product.StockQuantity
        });
    }

    // 🔹 POST: api/stock/decrease — уменьшить остаток (расход/заказ)
    [HttpPost("decrease")]
    // 🔹 УБРАНО: [Authorize(Roles = "Admin,Warehouse,Manager")]
    public async Task<IActionResult> DecreaseStock([FromBody] StockOperationDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return NotFound("Товар не найден");

        if (dto.Quantity <= 0)
            return BadRequest("Количество должно быть положительным");

        if (product.StockQuantity < dto.Quantity)
            return BadRequest($"Недостаточно товара на складе. Доступно: {product.StockQuantity}");

        // 🔹 Уменьшаем остаток
        product.StockQuantity -= dto.Quantity;
        product.UpdatedAt = DateTime.UtcNow;

        // 🔹 Записываем в историю
        var movement = new StockMovement
        {
            ProductId = dto.ProductId,
            UserId = dto.UserId,
            QuantityChange = -dto.Quantity,  // 🔹 Отрицательное значение = расход
            MovementType = dto.MovementType ?? "out",
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };
        _context.StockMovements.Add(movement);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"✅ Списано {dto.Quantity} шт. «{product.Name}»",
            newStockQuantity = product.StockQuantity
        });
    }

    // 🔹 PUT: api/stock/update — ручная корректировка остатка
    [HttpPut("update")]
    // 🔹 УБРАНО: [Authorize(Roles = "Admin,Warehouse")]
    public async Task<IActionResult> UpdateStock([FromBody] StockAdjustmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return NotFound("Товар не найден");

        if (dto.NewQuantity < 0)
            return BadRequest("Остаток не может быть отрицательным");

        // 🔹 Вычисляем изменение
        int change = dto.NewQuantity - product.StockQuantity;

        // 🔹 Обновляем остаток
        product.StockQuantity = dto.NewQuantity;
        product.UpdatedAt = DateTime.UtcNow;

        // 🔹 Записываем в историю (только если было изменение)
        if (change != 0)
        {
            var movement = new StockMovement
            {
                ProductId = dto.ProductId,
                UserId = dto.UserId,
                QuantityChange = change,
                MovementType = "adjustment",
                Comment = dto.Comment ?? $"Корректировка: {product.StockQuantity - change} → {dto.NewQuantity}",
                CreatedAt = DateTime.UtcNow
            };
            _context.StockMovements.Add(movement);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"✅ Остаток изменён: {dto.NewQuantity} шт.",
            previousQuantity = dto.NewQuantity - change,
            newStockQuantity = product.StockQuantity
        });
    }

    // 🔹 GET: api/stock/history/{productId} — история движений
    [HttpGet("history/{productId}")]
    // 🔹 УБРАНО: [Authorize]
    public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetStockHistory(int productId)
    {
        var movements = await _context.StockMovements
            .Where(m => m.ProductId == productId)
            .Include(m => m.User)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                ProductId = m.ProductId,
                UserName = m.User != null ? m.User.Login : "Unknown",
                QuantityChange = m.QuantityChange,
                MovementType = m.MovementType,
                Comment = m.Comment,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(movements);
    }

    // 🔹 GET: api/stock/summary — сводка по складу
    [HttpGet("summary")]
    // 🔹 УБРАНО: [Authorize(Roles = "Admin,Warehouse,Manager")]
    public async Task<ActionResult<StockSummaryDto>> GetStockSummary()
    {
        var totalProducts = await _context.Products.CountAsync(p => p.IsActive == true);
        var totalStockValue = await _context.Products
            .Where(p => p.IsActive == true)
            .SumAsync(p => p.Price * p.StockQuantity);
        var lowStockCount = await _context.Products
            .CountAsync(p => p.IsActive == true && p.StockQuantity <= p.MinStockThreshold);
        var outOfStockCount = await _context.Products
            .CountAsync(p => p.IsActive == true && p.StockQuantity == 0);

        return Ok(new StockSummaryDto
        {
            TotalProducts = totalProducts,
            TotalStockValue = totalStockValue,
            LowStockCount = lowStockCount,
            OutOfStockCount = outOfStockCount,
            GeneratedAt = DateTime.UtcNow
        });
    }
}

// ============================================
// 🔹 DTO для операций склада (без изменений)
// ============================================

public class StockOperationDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [Range(1, 999999)]
    public int Quantity { get; set; }

    [Required]
    public int UserId { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    // 🔹 Тип операции: in, out, order
    [StringLength(20)]
    public string? MovementType { get; set; }
}

public class StockAdjustmentDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [Range(0, 999999)]
    public int NewQuantity { get; set; }

    [Required]
    public int UserId { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }
}

public class StockMovementDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string UserName { get; set; } = null!;
    public int QuantityChange { get; set; }
    public string MovementType { get; set; } = null!;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StockSummaryDto
{
    public int TotalProducts { get; set; }
    public decimal TotalStockValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public DateTime GeneratedAt { get; set; }
}