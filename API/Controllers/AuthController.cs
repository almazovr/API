using API.DTOs;
using API.Models;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using BCrypt.Net;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DiplomContext _context;
    private readonly IEmailService _emailService;

    public AuthController(DiplomContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [HttpPost("request-code")]
    public async Task<IActionResult> RequestVerificationCode([FromBody] RegisterRequestDto dto)
    {
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (existingUser != null)
            return BadRequest(new { message = "Пользователь с таким email уже зарегистрирован" });

        var existingLogin = await _context.Users.FirstOrDefaultAsync(u => u.Login == dto.Login);
        if (existingLogin != null)
            return BadRequest(new { message = "Такой логин уже занят" });

        var code = GenerateVerificationCode();

        var oldCodes = await _context.EmailVerificationCodes
            .Where(c => c.Email == dto.Email && !c.IsUsed)
            .ToListAsync();
        _context.EmailVerificationCodes.RemoveRange(oldCodes);

        var verificationCode = new EmailVerificationCode
        {
            Email = dto.Email,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        };

        _context.EmailVerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync();

        await _emailService.SendVerificationCodeAsync(dto.Email, code, dto.Login);

        return Ok(new { message = "Код подтверждения отправлен на почту", expiresAt = verificationCode.ExpiresAt });
    }

    [HttpPost("verify-and-register")]
    public async Task<IActionResult> VerifyAndRegister([FromBody] VerifyRegisterDto dto)
    {
        var verification = await _context.EmailVerificationCodes
            .FirstOrDefaultAsync(c => c.Email == dto.Email && c.Code == dto.Code && !c.IsUsed);

        if (verification == null)
            return BadRequest(new { message = "Неверный или истёкший код подтверждения" });

        if (verification.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Срок действия кода истёк" });

        var user = new User
        {
            Login = dto.Login,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = 6,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        verification.IsUsed = true;

        await _context.SaveChangesAsync();

        return Ok(new RegisterResponseDto
        {
            Id = user.Id,
            Login = user.Login,
            Email = user.Email,
            RoleId = user.RoleId,
            CreatedAt = user.CreatedAt ?? DateTime.UtcNow
        });
    }

    private string GenerateVerificationCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
        return number.ToString("D6");
    }
}