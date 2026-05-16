using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RoleController : ControllerBase
{
    private readonly DiplomContext _context;
    public RoleController(DiplomContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> Get() =>
        await _context.Roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            CreatedAt = r.CreatedAt
        }).ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleDto>> Get(int id)
    {
        var role = await _context.Roles.Where(r => r.Id == id)
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name, Description = r.Description }).FirstOrDefaultAsync();
        return role == null ? NotFound() : Ok(role);
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Post(RoleDto dto)
    {
        var role = new Role { Name = dto.Name, Description = dto.Description, CreatedAt = DateTime.UtcNow };
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = role.Id }, new RoleDto { Id = role.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, RoleDto dto)
    {
        if (id != dto.Id) return BadRequest();
        var role = await _context.Roles.FindAsync(id);
        if (role == null) return NotFound();
        role.Name = dto.Name; role.Description = dto.Description;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null) return NotFound();
        if (_context.Users.Any(u => u.RoleId == id)) return BadRequest(new { message = "Роль используется" });
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? CreatedAt { get; set; }
}