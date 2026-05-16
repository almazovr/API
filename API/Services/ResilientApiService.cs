// 📁 API/Services/ResilientApiService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace API.Services
{
    public class ResilientApiService
    {
        // 🔹 СПИСОК СЕРВЕРОВ (Замените на реальные IP ваших ПК)
        // Используем ваши локальные сети. 
        // Пример: если API запущен на порту 5234 на разных машинах.
        private static readonly List<string> BackendServers = new()
        {
            "http://10.252.124.77:5234", // Ваш текущий ПК (Главный)
            "http://192.168.56.1:5234",  // Второй ПК (Резервный, если он в этой подсети)
            "http://localhost:5234"      // Локальный хост (для тестов)
        };

        private static string _currentWorkingServer = BackendServers[0];
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Универсальный метод GET с автоматическим переключением сервера
        /// </summary>
        public async Task<T?> GetAsync<T>(string endpoint)
        {
            Exception lastException = null;

            // Пробуем каждый сервер из списка
            foreach (var server in BackendServers)
            {
                try
                {
                    var url = $"{server.TrimEnd('/')}/{endpoint.TrimStart('/')}";
                    Debug.WriteLine($"📡 Попытка запроса к: {url}");

                    var response = await _httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();

                        // Если запрос успешен, запоминаем этот сервер как рабочий
                        if (_currentWorkingServer != server)
                        {
                            Debug.WriteLine($"✅ Сервер {server} работает! Переключаемся на него.");
                            _currentWorkingServer = server;
                        }

                        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.WriteLine($"❌ Сервер {server} не ответил: {ex.Message}");
                    continue; // Пробуем следующий сервер
                }
            }

            // Если ни один сервер не ответил
            Debug.WriteLine($"💥 КРИТИЧЕСКАЯ ОШИБКА: Ни один сервер не доступен!");
            throw new InvalidOperationException("Все серверы недоступны. Проверьте сеть.", lastException);
        }

        /// <summary>
        /// Универсальный метод POST
        /// </summary>
        public async Task<T?> PostAsync<T>(string endpoint, object data)
        {
            Exception lastException = null;

            foreach (var server in BackendServers)
            {
                try
                {
                    var url = $"{server.TrimEnd('/')}/{endpoint.TrimStart('/')}";
                    var json = JsonSerializer.Serialize(data, _jsonOptions);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        if (_currentWorkingServer != server)
                        {
                            Debug.WriteLine($"✅ Сервер {server} работает! Переключаемся на него.");
                            _currentWorkingServer = server;
                        }

                        var resultContent = await response.Content.ReadAsStringAsync();
                        return JsonSerializer.Deserialize<T>(resultContent, _jsonOptions);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.WriteLine($"❌ Сервер {server} не ответил: {ex.Message}");
                    continue;
                }
            }

            throw new InvalidOperationException("Все серверы недоступны.", lastException);
        }

        /// <summary>
        /// Получить текущий активный сервер (для отладки)
        /// </summary>
        public string GetCurrentServer() => _currentWorkingServer;
    }
}