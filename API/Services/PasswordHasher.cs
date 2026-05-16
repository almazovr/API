using BCrypt.Net;

namespace API.Services;

public static class PasswordHasher
{
    /// <summary>
    /// Хеширует пароль с использованием BCrypt
    /// </summary>
    public static string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// Проверяет пароль против хеша
    /// </summary>
    public static bool Verify(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}