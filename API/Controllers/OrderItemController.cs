// 📁 API/Controllers/OrderItemController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderItemController : ControllerBase
{
    private readonly DiplomContext _context;
    public OrderItemController(DiplomContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderItemDto>>> Get() =>
        await _context.OrderItems
            .Include(oi => oi.Product)
            .Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                OrderId = oi.OrderId,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                ProductBrand = oi.ProductBrand,
                Quantity = oi.Quantity,
                PriceAtTime = oi.PriceAtTime,
                CreatedAt = oi.CreatedAt
            })
            .ToListAsync();

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<IEnumerable<OrderItemDto>>> GetByOrderId(int orderId)
    {
        var items = await _context.OrderItems
            .Where(oi => oi.OrderId == orderId)
            .Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                OrderId = oi.OrderId,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                ProductBrand = oi.ProductBrand,
                Quantity = oi.Quantity,
                PriceAtTime = oi.PriceAtTime,
                CreatedAt = oi.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderItemDto>> Get(int id)
    {
        var item = await _context.OrderItems
            .Where(oi => oi.Id == id)
            .Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                OrderId = oi.OrderId,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                ProductBrand = oi.ProductBrand,
                Quantity = oi.Quantity,
                PriceAtTime = oi.PriceAtTime,
                CreatedAt = oi.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<OrderItemDto>> Post([FromBody] OrderItemDto dto)
    {
        var item = new OrderItem
        {
            OrderId = dto.OrderId,
            ProductId = dto.ProductId,
            ProductName = dto.ProductName,
            ProductBrand = dto.ProductBrand,
            Quantity = dto.Quantity,
            PriceAtTime = dto.PriceAtTime,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = item.Id }, new OrderItemDto { Id = item.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] OrderItemDto dto)
    {
        if (id != dto.Id) return BadRequest();

        var item = await _context.OrderItems.FindAsync(id);
        if (item == null) return NotFound();

        item.Quantity = dto.Quantity;
        item.PriceAtTime = dto.PriceAtTime;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.OrderItems.FindAsync(id);
        if (item == null) return NotFound();

        _context.OrderItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

// ============================================================================
// 🔹 DTO для OrderItemController (определён в этом же файле)
// ============================================================================

public class OrderItemDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int? ProductId { get; set; }  // 🔹 nullable, как в модели БД
    public string? ProductName { get; set; }
    public string? ProductBrand { get; set; }
    public int Quantity { get; set; }
    public decimal PriceAtTime { get; set; }
    public DateTime? CreatedAt { get; set; }
}