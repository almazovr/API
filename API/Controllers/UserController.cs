using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;
using API.Services; // 👈 Добавьте этот using

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly DiplomContext _context;
    public UserController(DiplomContext context) => _context = context;

    // GET: api/User
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> Get() =>
        await _context.Users
            .Include(u => u.Role)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Login = u.Login,
                Email = u.Email,
                Phone = u.Phone,
                RoleId = u.RoleId,
                RoleName = u.Role != null ? u.Role.Name : null,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
                // 🔐 PasswordHash НЕ возвращаем в ответе!
            }).ToListAsync();

    // GET: api/User/5
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        var user = await _context.Users.Include(u => u.Role)
            .Where(u => u.Id == id)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Login = u.Login,
                Email = u.Email,
                Phone = u.Phone,
                RoleId = u.RoleId,
                RoleName = u.Role != null ? u.Role.Name : null,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).FirstOrDefaultAsync();

        return user == null ? NotFound() : Ok(user);
    }

    
    [HttpPost]
    public async Task<ActionResult<UserDto>> Post(UserCreateDto dto)
    {
        
        string hashedPassword = PasswordHasher.Hash(dto.Password);

        var user = new User
        {
            Login = dto.Login,
            Email = dto.Email,
            PasswordHash = hashedPassword,  
            Phone = dto.Phone,
            RoleId = dto.RoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = user.Id }, new UserDto
        {
            Id = user.Id,
            Login = user.Login,
            Email = user.Email
        });
    }

    // POST: api/User/login (вход)
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Login == dto.Login);

        // 🔐 Проверяем логин и пароль
        if (user == null || !PasswordHasher.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Неверный логин или пароль" });
        }

        // 🔐 Проверяем, активен ли пользователь
        if (user.IsActive == false)
        {
            return Unauthorized(new { message = "Аккаунт заблокирован" });
        }

        // ✅ Успешный вход — возвращаем данные без пароля
        return Ok(new LoginResponseDto
        {
            Id = user.Id,
            Login = user.Login,
            Email = user.Email,
            RoleId = user.RoleId,
            RoleName = user.Role?.Name,
            Token = GenerateSimpleToken(user.Id) // 🔹 Простой токен (для диплома)
        });
    }

    // 🔹 Простая генерация токена (для диплома)
    // ⚠️ В реальном проекте используйте JWT: https://learn.microsoft.com/ru-ru/aspnet/core/security/authentication/jwt-auth
    private string GenerateSimpleToken(int userId)
    {
        var random = new Random();
        var bytes = new byte[32];
        random.NextBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    // PUT: api/User/5 (обновление)
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, UserUpdateDto dto)
    {
        if (id != dto.Id) return BadRequest();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Login = dto.Login;
        user.Email = dto.Email;
        user.Phone = dto.Phone;
        user.RoleId = dto.RoleId;
        user.IsActive = dto.IsActive;

        // 🔐 Если передан новый пароль — хешируем и обновляем
        if (!string.IsNullOrEmpty(dto.NewPassword))
        {
            user.PasswordHash = PasswordHasher.Hash(dto.NewPassword);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/User/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

// 🔹 DTO для ответа при входе (без пароля!)
public class LoginResponseDto
{
    public int Id { get; set; }
    public string Login { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? RoleId { get; set; }
    public string? RoleName { get; set; }
    public string Token { get; set; } = null!; // 🔹 Токен для авторизации
}

// 🔹 DTO для входа (только логин + пароль)
public class LoginDto
{
    public string Login { get; set; } = null!;
    public string Password { get; set; } = null!;
}

// 🔹 DTO для создания пользователя (пароль отдельно)
public class UserCreateDto
{
    public string Login { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!; // 👈 Пароль для хеширования
    public string? Phone { get; set; }
    public int? RoleId { get; set; }
}

// 🔹 DTO для обновления пользователя (новый пароль — опционально)
public class UserUpdateDto
{
    public int Id { get; set; }
    public string Login { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public int? RoleId { get; set; }
    public bool? IsActive { get; set; }
    public string? NewPassword { get; set; } // 👈 Новый пароль (если нужно сменить)
}

// 🔹 Старый DTO для GET-запросов (без пароля)
public class UserDto
{
    public int Id { get; set; }
    public string Login { get; set; } = null!;
    public string Email { get; set; } = null!;
    // 🔐 PasswordHash убран — не возвращаем хеши клиентам!
    public string? Phone { get; set; }
    public int? RoleId { get; set; }
    public string? RoleName { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
}