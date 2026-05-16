// 📁 API/Controllers/OrderStatusController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderStatusController : ControllerBase
{
    private readonly DiplomContext _context;
    public OrderStatusController(DiplomContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderStatusDto>>> Get() =>
        await _context.OrderStatuses
            .Select(s => new OrderStatusDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderStatusDto>> Get(int id)
    {
        var status = await _context.OrderStatuses
            .Where(s => s.Id == id)
            .Select(s => new OrderStatusDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                CreatedAt = s.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (status == null) return NotFound();
        return Ok(status);
    }

    [HttpPost]
    public async Task<ActionResult<OrderStatusDto>> Post([FromBody] OrderStatusDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Название статуса обязательно" });

        var status = new OrderStatus
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderStatuses.Add(status);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = status.Id }, new OrderStatusDto { Id = status.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] OrderStatusDto dto)
    {
        if (id != dto.Id) return BadRequest();

        var status = await _context.OrderStatuses.FindAsync(id);
        if (status == null) return NotFound();

        status.Name = dto.Name;
        status.Description = dto.Description;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var status = await _context.OrderStatuses.FindAsync(id);
        if (status == null) return NotFound();

        if (await _context.Orders.AnyAsync(o => o.StatusId == id))
            return BadRequest(new { message = "Невозможно удалить: статус используется в заказах" });

        _context.OrderStatuses.Remove(status);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class OrderStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? CreatedAt { get; set; }
}