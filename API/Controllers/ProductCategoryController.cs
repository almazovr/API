using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductCategoryController : ControllerBase
{
    private readonly DiplomContext _context;

    public ProductCategoryController(DiplomContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductCategory>>> GetCategories() =>
        await _context.ProductCategories
            .OrderBy(c => c.Order)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductCategory>> GetCategory(int id)
    {
        var category = await _context.ProductCategories.FindAsync(id);
        return category == null ? NotFound() : Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<ProductCategory>> PostCategory(ProductCategory category)
    {
        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutCategory(int id, ProductCategory category)
    {
        if (id != category.Id) return BadRequest();

        var existing = await _context.ProductCategories.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = category.Name;
        existing.Description = category.Description;
        existing.Order = category.Order;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.ProductCategories.FindAsync(id);
        if (category == null) return NotFound();

        if (_context.Products.Any(p => p.CategoryId == id))
            return BadRequest(new { message = "Нельзя удалить категорию с товарами" });

        _context.ProductCategories.Remove(category);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}