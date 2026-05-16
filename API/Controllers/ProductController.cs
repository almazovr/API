// 📁 API/Controllers/ProductController.cs
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductController : ControllerBase
{
    private readonly DiplomContext _context;

    public ProductController(DiplomContext context) => _context = context;

    // 🔹 GET: api/product — с полями склада
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Price = p.Price,
                NewPrice = p.NewPrice,
                Image = p.Image,
                Description = p.Description,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                Attributes = p.Attributes,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                // 🔹 🔹 🔹 НОВЫЕ ПОЛЯ СКЛАДА
                StockQuantity = p.StockQuantity,
                MinStockThreshold = p.MinStockThreshold
            })
            .OrderBy(p => p.Name)
            .ToListAsync();

        return Ok(products);
    }

    // 🔹 GET: api/product/{id} — с полями склада
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Id == id)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Price = p.Price,
                NewPrice = p.NewPrice,
                Image = p.Image,
                Description = p.Description,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                Attributes = p.Attributes,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                // 🔹 🔹 🔹 НОВЫЕ ПОЛЯ СКЛАДА
                StockQuantity = p.StockQuantity,
                MinStockThreshold = p.MinStockThreshold
            })
            .FirstOrDefaultAsync();

        return product == null ? NotFound() : Ok(product);
    }

    // 🔹 GET: api/product/low-stock — товары с низким остатком
    [HttpGet("low-stock")]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetLowStockProducts()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.StockQuantity <= p.MinStockThreshold)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Price = p.Price,
                NewPrice = p.NewPrice,
                Image = p.Image,
                Description = p.Description,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                Attributes = p.Attributes,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                StockQuantity = p.StockQuantity,
                MinStockThreshold = p.MinStockThreshold
            })
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();

        return Ok(products);
    }

    // 🔹 POST: api/product — создание с начальным остатком
    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = new Product
        {
            Name = dto.Name,
            Brand = dto.Brand,
            Price = dto.Price,
            NewPrice = dto.NewPrice ?? 0,
            Image = dto.Image,
            Description = dto.Description,
            CategoryId = dto.CategoryId,
            Attributes = dto.Attributes,
            IsActive = dto.IsActive ?? true,
            // 🔹 🔹 🔹 НОВЫЕ ПОЛЯ СКЛАДА
            StockQuantity = dto.StockQuantity ?? 0,
            MinStockThreshold = dto.MinStockThreshold ?? 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var result = new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Brand = product.Brand,
            Price = product.Price,
            NewPrice = product.NewPrice,
            Image = product.Image,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            Attributes = product.Attributes,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            StockQuantity = product.StockQuantity,
            MinStockThreshold = product.MinStockThreshold
        };

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, result);
    }

    // 🔹 PUT: api/product/{id} — обновление с полями склада
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 🔹 🔹 🔹 ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА: явная валидация длины строк
        if (dto.Brand?.Length > 255)
            return BadRequest(new { message = "Название бренда не должно превышать 255 символов" });
        if (dto.Name?.Length > 200)
            return BadRequest(new { message = "Название товара не должно превышать 200 символов" });
        if (dto.Image?.Length > 500)
            return BadRequest(new { message = "URL изображения не должен превышать 500 символов" });

        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Brand != null) product.Brand = dto.Brand;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.NewPrice.HasValue) product.NewPrice = dto.NewPrice.Value;
        if (dto.Image != null) product.Image = dto.Image;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.CategoryId.HasValue) product.CategoryId = dto.CategoryId;
        if (dto.Attributes != null) product.Attributes = dto.Attributes;
        if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;
        // 🔹 🔹 🔹 НОВЫЕ ПОЛЯ СКЛАДА
        if (dto.StockQuantity.HasValue) product.StockQuantity = dto.StockQuantity.Value;
        if (dto.MinStockThreshold.HasValue) product.MinStockThreshold = dto.MinStockThreshold.Value;

        product.UpdatedAt = DateTime.UtcNow;

        _context.Products.Update(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // 🔹 DELETE: api/product/{id} — мягкое удаление
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        _context.Products.Update(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

// ============================================
// 🔹 DTO-классы с полями склада (ИСПРАВЛЕНО)
// ============================================

public class CreateProductDto
{
    [Required]
    [StringLength(200, ErrorMessage = "Название товара должно содержать от 2 до 200 символов", MinimumLength = 2)]
    public string Name { get; set; } = null!;

    // 🔹 🔹 🔹 ИСПРАВЛЕНО: 100 → 255
    [StringLength(255, ErrorMessage = "Название бренда не должно превышать 255 символов")]
    public string? Brand { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "Цена должна быть от 0.01 до 999999.99")]
    public decimal Price { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "Цена со скидкой должна быть от 0.01 до 999999.99")]
    public decimal? NewPrice { get; set; }

    [StringLength(500, ErrorMessage = "URL изображения не должен превышать 500 символов")]
    public string? Image { get; set; }

    [StringLength(2000, ErrorMessage = "Описание не должно превышать 2000 символов")]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }

    public string? Attributes { get; set; }

    public bool? IsActive { get; set; }

    // 🔹 🔹 🔹 НОВЫЕ ПОЛЯ СКЛАДА
    [Range(0, 999999, ErrorMessage = "Количество на складе должно быть от 0 до 999999")]
    public int? StockQuantity { get; set; } = 0;

    [Range(0, 999999, ErrorMessage = "Порог остатка должен быть от 0 до 999999")]
    public int? MinStockThreshold { get; set; } = 5;
}

public class UpdateProductDto
{
    [StringLength(200, ErrorMessage = "Название товара должно содержать от 2 до 200 символов", MinimumLength = 2)]
    public string? Name { get; set; }

    // 🔹 🔹 🔹 ИСПРАВЛЕНО: 100 → 255
    [StringLength(255, ErrorMessage = "Название бренда не должно превышать 255 символов")]
    public string? Brand { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "Цена должна быть от 0.01 до 999999.99")]
    public decimal? Price { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "Цена со скидкой должна быть от 0.01 до 999999.99")]
    public decimal? NewPrice { get; set; }

    [StringLength(500, ErrorMessage = "URL изображения не должен превышать 500 символов")]
    public string? Image { get; set; }

    [StringLength(2000, ErrorMessage = "Описание не должно превышать 2000 символов")]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }

    public string? Attributes { get; set; }

    public bool? IsActive { get; set; }

    // 🔹 🔹 🔹 НОВЫЕ ПОЛЯ СКЛАДА
    [Range(0, 999999, ErrorMessage = "Количество на складе должно быть от 0 до 999999")]
    public int? StockQuantity { get; set; }

    [Range(0, 999999, ErrorMessage = "Порог остатка должен быть от 0 до 999999")]
    public int? MinStockThreshold { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Brand { get; set; }
    public decimal Price { get; set; }
    public decimal? NewPrice { get; set; }
    public string? Image { get; set; }
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Attributes { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // 🔹 🔹 🔹 НОВЫЕ ПОЛЯ СКЛАДА
    public int StockQuantity { get; set; }
    public int MinStockThreshold { get; set; }
}