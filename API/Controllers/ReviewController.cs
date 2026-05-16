// 📁 API/Controllers/ReviewController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;
using API.DTOs;  // 🔹 Если DTO лежат в API.DTOs, иначе используйте DIPLOM.Models

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReviewController : ControllerBase
{
    private readonly DiplomContext _context;

    public ReviewController(DiplomContext context) => _context = context;

    // 🔹 GET: Все отзывы (для админа)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> Get() =>
        await _context.Reviews
            .Include(r => r.User)
            .Include(r => r.Product)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserName = r.User != null ? r.User.Login : "Неизвестно",
                ProductId = r.ProductId,
                ProductName = r.Product != null ? r.Product.Name : "Удалённый товар",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

    // 🔹 GET: Отзывы для конкретного товара
    [HttpGet("product/{productId}")]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetByProductId(int productId) =>
        await _context.Reviews
            .Include(r => r.User)
            .Include(r => r.Product)
            .Where(r => r.ProductId == productId)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserName = r.User != null ? r.User.Login : "Неизвестно",
                ProductId = r.ProductId,
                ProductName = r.Product != null ? r.Product.Name : "Удалённый товар",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

    // 🔹 GET: Средний рейтинг для товара
    [HttpGet("product/{productId}/average")]
    public async Task<ActionResult<double>> GetAverageRating(int productId)
    {
        var avg = await _context.Reviews
            .Where(r => r.ProductId == productId)
            .AverageAsync(r => (double?)r.Rating) ?? 0;

        return Ok(avg);
    }

    // 🔹 POST: Создать новый отзыв
    [HttpPost]
    public async Task<ActionResult<ReviewDto>> Post([FromBody] ReviewCreateDto dto)
    {
        // 🔹 Валидация
        if (dto.UserId <= 0 || dto.ProductId <= 0)
            return BadRequest(new { message = "UserId и ProductId обязательны" });

        if (dto.Rating < 1 || dto.Rating > 5)
            return BadRequest(new { message = "Рейтинг должен быть от 1 до 5" });

        // 🔹 Проверка существования товара
        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return NotFound(new { message = "Товар не найден" });

        // 🔹 Проверка существования пользователя
        var user = await _context.Users.FindAsync(dto.UserId);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        // 🔹 Проверка: пользователь уже оставлял отзыв на этот товар?
        var existingReview = await _context.Reviews
            .FirstOrDefaultAsync(r => r.UserId == dto.UserId && r.ProductId == dto.ProductId);

        if (existingReview != null)
        {
            // 🔹 Если отзыв уже есть — обновляем его
            existingReview.Rating = dto.Rating;
            existingReview.Comment = dto.Comment;
            existingReview.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            // 🔹 Создаём новый отзыв
            var review = new Review
            {
                UserId = dto.UserId,
                ProductId = dto.ProductId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reviews.Add(review);
        }

        await _context.SaveChangesAsync();

        // 🔹 Возвращаем созданный/обновлённый отзыв
        var result = await _context.Reviews
            .Include(r => r.User)
            .Include(r => r.Product)
            .Where(r => r.UserId == dto.UserId && r.ProductId == dto.ProductId)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserName = r.User != null ? r.User.Login : "Неизвестно",
                ProductId = r.ProductId,
                ProductName = r.Product != null ? r.Product.Name : "Удалённый товар",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .FirstOrDefaultAsync();

        return result != null
            ? CreatedAtAction(nameof(GetByProductId), new { productId = dto.ProductId }, result)
            : StatusCode(500, new { message = "Ошибка при сохранении отзыва" });
    }

    // 🔹 PUT: Обновить отзыв
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] ReviewUpdateDto dto)
    {
        if (id != dto.Id) return BadRequest();

        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        review.Rating = dto.Rating;
        review.Comment = dto.Comment;
        review.CreatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Reviews.Any(e => e.Id == id))
                return NotFound();
            throw;
        }

        return NoContent();
    }

    // 🔹 DELETE: Удалить отзыв (только для админа)
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}