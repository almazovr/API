// 📁 API/Controllers/CartItemController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CartItemController : ControllerBase
{
    private readonly DiplomContext _context;
    public CartItemController(DiplomContext context) => _context = context;

    // 🔹 GET: Все элементы корзины (для админа)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CartItemDto>>> GetAll() =>
        await _context.CartItems
            .Include(ci => ci.Product)
            .Select(ci => new CartItemDto
            {
                Id = ci.Id,
                CartId = ci.CartId,
                ProductId = ci.ProductId,
                ProductName = ci.Product != null ? ci.Product.Name : null,
                ProductImage = ci.Product != null ? ci.Product.Image : null,
                ProductPrice = ci.Product != null ? ci.Product.Price : 0,
                Quantity = ci.Quantity,
                AddedAt = ci.AddedAt
            }).ToListAsync();

    // 🔹 НОВОЕ: Получить товары корзины ПОЛЬЗОВАТЕЛЯ
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<CartItemDto>>> GetByUserId(int userId)
    {
        // Находим корзину пользователя
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
            return Ok(new List<CartItemDto>()); // Пустая корзина — это нормально

        // Возвращаем товары этой корзины с данными продукта
        var items = await _context.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CartId == cart.Id)
            .Select(ci => new CartItemDto
            {
                Id = ci.Id,
                CartId = ci.CartId,
                ProductId = ci.ProductId,
                ProductName = ci.Product != null ? ci.Product.Name : null,
                ProductImage = ci.Product != null ? ci.Product.Image : null,
                ProductPrice = ci.Product != null ? ci.Product.Price : 0,
                Quantity = ci.Quantity,
                AddedAt = ci.AddedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    // 🔹 POST: Добавить товар в корзину (с проверкой дубликатов)
    [HttpPost]
    public async Task<ActionResult<CartItemDto>> AddToCart(CartItemAddDto dto)
    {
        // Находим корзину пользователя
        var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == dto.UserId);
        if (cart == null)
        {
            // Создаём новую корзину, если нет
            cart = new Cart
            {
                UserId = dto.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        // Проверяем, есть ли уже такой товар в корзине
        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == dto.ProductId);

        if (existingItem != null)
        {
            // Увеличиваем количество
            existingItem.Quantity += dto.Quantity;
            existingItem.AddedAt = DateTime.UtcNow;
        }
        else
        {
            // Создаём новый элемент
            var cartItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                AddedAt = DateTime.UtcNow
            };
            _context.CartItems.Add(cartItem);
        }

        await _context.SaveChangesAsync();

        // Возвращаем обновлённый элемент
        var result = await _context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == dto.ProductId);

        if (result == null)
            return BadRequest("Не удалось создать элемент корзины");

        return Ok(new CartItemDto
        {
            Id = result.Id,
            CartId = result.CartId,
            ProductId = result.ProductId,
            ProductName = result.Product?.Name,
            ProductImage = result.Product?.Image,
            ProductPrice = result.Product?.Price ?? 0,
            Quantity = result.Quantity,
            AddedAt = result.AddedAt
        });
    }

    // 🔹 PUT: Обновить количество
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CartItemUpdateDto dto)
    {
        var item = await _context.CartItems.FindAsync(id);
        if (item == null) return NotFound();

        item.Quantity = dto.Quantity;
        item.AddedAt = DateTime.UtcNow;

        _context.Entry(item).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // 🔹 DELETE: Удалить товар из корзины
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.CartItems.FindAsync(id);
        if (item == null) return NotFound();

        _context.CartItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // 🔹 DELETE: Очистить всю корзину пользователя
    [HttpDelete("user/{userId}")]
    public async Task<IActionResult> ClearCart(int userId)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.CartItems.Any())
            return NotFound();

        _context.CartItems.RemoveRange(cart.CartItems);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

// 🔹 DTO для добавления товара в корзину
public class CartItemAddDto
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

// 🔹 DTO для обновления количества
public class CartItemUpdateDto
{
    public int Quantity { get; set; }
}

// 🔹 DTO для ответа (с данными продукта)
public class CartItemDto
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductImage { get; set; }
    public decimal ProductPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice => ProductPrice * Quantity;
    public DateTime? AddedAt { get; set; }
}