// 📁 API/Controllers/FavoriteController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FavoriteController : ControllerBase
{
    private readonly DiplomContext _context;

    public FavoriteController(DiplomContext context) => _context = context;

    // 🔹 GET: Все избранные (для админа)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FavoriteDto>>> GetAll() =>
        await _context.Favorites
            .Include(f => f.User)
            .Include(f => f.Product)
            .Select(f => new FavoriteDto
            {
                Id = f.Id,
                UserId = f.UserId,
                UserName = f.User != null ? f.User.Login : null,
                ProductId = f.ProductId,
                ProductName = f.Product != null ? f.Product.Name : null,
                ProductImage = f.Product != null ? f.Product.Image : null,
                ProductPrice = f.Product != null ? f.Product.Price : 0,
                ProductBrand = f.Product != null ? f.Product.Brand : null,
                AddedAt = f.AddedAt
            }).ToListAsync();

    // 🔹 НОВОЕ: Получить избранное ПОЛЬЗОВАТЕЛЯ (основной эндпоинт для клиента)
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<FavoriteDto>>> GetByUserId(int userId)
    {
        var favorites = await _context.Favorites
            .Include(f => f.Product)
            .Where(f => f.UserId == userId)
            .Select(f => new FavoriteDto
            {
                Id = f.Id,
                UserId = f.UserId,
                ProductId = f.ProductId,
                ProductName = f.Product != null ? f.Product.Name : null,
                ProductImage = f.Product != null ? f.Product.Image : null,
                ProductPrice = f.Product != null ? f.Product.Price : 0,
                ProductBrand = f.Product != null ? f.Product.Brand : null,
                AddedAt = f.AddedAt
            })
            .ToListAsync();

        return Ok(favorites);
    }

    [HttpPost]
    public async Task<ActionResult<FavoriteDto>> AddToFavorite([FromBody] FavoriteAddDto dto)
    {
        var existing = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == dto.UserId && f.ProductId == dto.ProductId);

        if (existing != null)
        {
            var existingProduct = await _context.Products.FindAsync(dto.ProductId);
            return Ok(new FavoriteDto
            {
                Id = existing.Id,
                UserId = existing.UserId,
                ProductId = existing.ProductId,
                ProductName = existingProduct?.Name,
                ProductImage = existingProduct?.Image,
                ProductPrice = existingProduct?.Price ?? 0,
                ProductBrand = existingProduct?.Brand,
                AddedAt = existing.AddedAt
            });
        }

        var fav = new Favorite
        {
            UserId = dto.UserId,
            ProductId = dto.ProductId,
            AddedAt = DateTime.UtcNow
        };

        _context.Favorites.Add(fav);
        await _context.SaveChangesAsync();

        // Возвращаем данные с информацией о продукте
        var newProduct = await _context.Products.FindAsync(dto.ProductId); // 🔹 Переименовано в newProduct

        return CreatedAtAction(nameof(GetByUserId), new { userId = dto.UserId }, new FavoriteDto
        {
            Id = fav.Id,
            UserId = fav.UserId,
            ProductId = fav.ProductId,
            ProductName = newProduct?.Name,
            ProductImage = newProduct?.Image,
            ProductPrice = newProduct?.Price ?? 0,
            ProductBrand = newProduct?.Brand,
            AddedAt = fav.AddedAt
        });
    }

    // 🔹 DELETE: Удалить товар из избранного
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var fav = await _context.Favorites.FindAsync(id);
        if (fav == null) return NotFound();

        _context.Favorites.Remove(fav);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // 🔹 НОВОЕ: Проверить, есть ли товар в избранном
    [HttpGet("check")]
    public async Task<ActionResult<bool>> IsInFavorite([FromQuery] int userId, [FromQuery] int productId)
    {
        var exists = await _context.Favorites
            .AnyAsync(f => f.UserId == userId && f.ProductId == productId);

        return Ok(exists);
    }
}

// 🔹 DTO для добавления в избранное
public class FavoriteAddDto
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
}

// 🔹 DTO для ответа (с данными продукта)
public class FavoriteDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductImage { get; set; }
    public decimal ProductPrice { get; set; }
    public string? ProductBrand { get; set; }
    public DateTime? AddedAt { get; set; }
}