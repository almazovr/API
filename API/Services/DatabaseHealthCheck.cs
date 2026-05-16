// 📁 API/Services/DatabaseHealthCheck.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Services
{
    public interface IDatabaseHealthCheck
    {
        string GetWorkingConnectionString();
        Task<bool> CheckDatabaseConnectionAsync();
        string GetCurrentConnectionString();
    }

    public class DatabaseHealthCheck : IDatabaseHealthCheck
    {
        private readonly IConfiguration _config;
        private readonly ILogger<DatabaseHealthCheck> _logger;
        private string _currentConnectionString;
        private DateTime _lastCheckTime;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public DatabaseHealthCheck(IConfiguration config, ILogger<DatabaseHealthCheck> logger)
        {
            _config = config;
            _logger = logger;
            _currentConnectionString = string.Empty;
            _lastCheckTime = DateTime.MinValue;
        }

        /// <summary>
        /// Получить рабочую строку подключения (проверяет раз в 5 минут)
        /// </summary>
        public string GetWorkingConnectionString()
        {
            // Если уже есть рабочее подключение и прошло меньше 5 минут — возвращаем его
            if (!string.IsNullOrEmpty(_currentConnectionString) &&
                DateTime.Now - _lastCheckTime < _checkInterval)
            {
                return _currentConnectionString;
            }

            // Иначе пробуем найти рабочее
            return FindWorkingConnection();
        }

        /// <summary>
        /// Текущая активная строка подключения
        /// </summary>
        public string GetCurrentConnectionString() => _currentConnectionString;

        /// <summary>
        /// Проверка подключения к БД
        /// </summary>
        public async Task<bool> CheckDatabaseConnectionAsync()
        {
            try
            {
                var connStr = GetWorkingConnectionString();
                if (string.IsNullOrEmpty(connStr))
                    return false;

                await using var conn = new NpgsqlConnection(connStr);
                await conn.OpenAsync();

                // Простой запрос для проверки
                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();

                _logger.LogInformation("✅ Подключение к БД активно: {Host}", GetHostFromConnectionString(connStr));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка подключения к БД");
                return false;
            }
        }

        /// <summary>
        /// Найти рабочее подключение перебором
        /// </summary>
        private string FindWorkingConnection()
        {
            // Получаем все строки подключения
            var connections = new List<string>();

            // Основная строка
            var defaultConn = _config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(defaultConn))
                connections.Add(defaultConn);

            // Дополнительные (fallback)
            var fallbackSection = _config.GetSection("ConnectionStrings:FallbackConnections");
            if (fallbackSection.Exists())
            {
                var fallbackConnections = fallbackSection.Get<string[]>();
                if (fallbackConnections != null)
                    connections.AddRange(fallbackConnections);
            }

            _logger.LogInformation("🔍 Проверка {Count} подключений к БД...", connections.Count);

            // Пробуем каждое подключение
            foreach (var connStr in connections)
            {
                try
                {
                    _logger.LogDebug("📡 Пробую: {ConnStr}", MaskConnectionString(connStr));

                    using var conn = new NpgsqlConnection(connStr);
                    conn.Open();

                    // Простой запрос для проверки
                    using var cmd = new NpgsqlCommand("SELECT 1", conn);
                    cmd.ExecuteScalar();

                    // Успех!
                    _currentConnectionString = connStr;
                    _lastCheckTime = DateTime.Now;

                    _logger.LogInformation("✅ Подключение успешно: {Host}", GetHostFromConnectionString(connStr));
                    return connStr;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("❌ Не удалось: {Host} - {Error}",
                        GetHostFromConnectionString(connStr),
                        ex.Message);
                }
            }

            // Ни одно подключение не сработало
            _logger.LogError("💥 Ни одно подключение к БД не работает!");
            return string.Empty;
        }

        /// <summary>
        /// Извлечь хост из строки подключения (для логирования)
        /// </summary>
        private string GetHostFromConnectionString(string connStr)
        {
            var hostParam = connStr.Split(';')
                .FirstOrDefault(p => p.StartsWith("Host=", StringComparison.OrdinalIgnoreCase));
            return hostParam?.Substring(5) ?? "unknown";
        }

        /// <summary>
        /// Замаскировать пароль в строке подключения (для логов)
        /// </summary>
        private string MaskConnectionString(string connStr)
        {
            return connStr.Replace("Password=" + GetPasswordFromConnectionString(connStr), "Password=***");
        }

        private string GetPasswordFromConnectionString(string connStr)
        {
            var passParam = connStr.Split(';')
                .FirstOrDefault(p => p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase));
            return passParam?.Substring(9) ?? "";
        }
    }
}