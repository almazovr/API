// 📁 API/Services/DatabaseMonitorService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace API.Services
{
    public class DatabaseMonitorService : BackgroundService
    {
        private readonly IDatabaseHealthCheck _healthCheck;
        private readonly ILogger<DatabaseMonitorService> _logger;
        private readonly TimeSpan _checkInterval;

        public DatabaseMonitorService(
            IDatabaseHealthCheck healthCheck,
            ILogger<DatabaseMonitorService> logger,
            TimeSpan? checkInterval = null)
        {
            _healthCheck = healthCheck;
            _logger = logger;
            _checkInterval = checkInterval ?? TimeSpan.FromMinutes(2);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(" DatabaseMonitor запущен. Проверка каждые {_interval}", _checkInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var isHealthy = await _healthCheck.CheckDatabaseConnectionAsync();

                    if (!isHealthy)
                    {
                        _logger.LogWarning("⚠️ БД недоступна! Пробую переподключиться...");
                        // Принудительно ищем новое подключение
                        var method = _healthCheck.GetType().GetMethod("FindWorkingConnection",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        method?.Invoke(_healthCheck, null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Ошибка в DatabaseMonitor");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}