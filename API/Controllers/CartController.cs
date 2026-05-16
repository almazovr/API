// 📁 API/Controllers/CartController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CartController : ControllerBase
{
    private readonly DiplomContext _context;
    public CartController(DiplomContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CartDto>>> Get() =>
        await _context.Carts
            .Include(c => c.User)
            .Select(c => new CartDto
            {
                Id = c.Id,
                UserId = c.UserId,
                UserName = c.User != null ? c.User.Login : null,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<CartDto>> Post(CartDto dto)
    {
        var existing = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == dto.UserId);
        if (existing != null)
            return Ok(new CartDto { Id = existing.Id, UserId = existing.UserId });

        var cart = new Cart
        {
            UserId = dto.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = cart.Id }, new CartDto { Id = cart.Id });
    }
}

public class CartDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}