using API.Models.YooKassa;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Services;

public class YooKassaService : IYooKassaService
{
    private readonly HttpClient _httpClient;
    private readonly string _shopId;
    private readonly string _secretKey;
    private readonly string? _webhookSecret;
    private readonly ILogger<YooKassaService> _logger;

    private const string ApiBaseUrl = "https://api.yookassa.ru/v3/";
    private const string DefaultReturnUrl = "https://example.com";

    // 🔹 🔹 🔹 ОБЯЗАТЕЛЬНО: настройки сериализации snake_case
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public YooKassaService(
        IConfiguration config,
        HttpClient httpClient,
        ILogger<YooKassaService> logger)
    {
        _shopId = config["YooKassa:ShopId"]
            ?? throw new InvalidOperationException("YooKassa:ShopId не настроен");
        _secretKey = config["YooKassa:SecretKey"]
            ?? throw new InvalidOperationException("YooKassa:SecretKey не настроен");
        _webhookSecret = config["YooKassa:WebhookSecret"];

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Clear();

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_shopId}:{_secretKey}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger = logger;
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("💳 Создание платежа: OrderId={OrderId}, Amount={Amount}",
            request.OrderId, request.Amount);

        var paymentPayload = new
        {
            amount = new
            {
                value = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
                currency = request.Currency.ToUpperInvariant()
            },
            capture = true,
            confirmation = new
            {
                type = "redirect",
                return_url = DefaultReturnUrl
            },
            description = TruncateDescription(request.Description),
            metadata = new { order_id = request.OrderId }
        };

        try
        {
            // 🔹 🔹 🔹 ГЕНЕРАЦИЯ IDEMPOTENCE-KEY
            var idempotenceKey = Guid.NewGuid().ToString();

            // 🔹 Создаём запрос вручную для точного контроля заголовков
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "payments")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(paymentPayload, _jsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            // 🔹 Добавляем заголовок идемпотентности
            requestMessage.Headers.Add("Idempotence-Key", idempotenceKey);

            _logger.LogDebug("🔑 Idempotence-Key: {Key}", idempotenceKey);
            _logger.LogDebug("📤 Отправляем: {Json}", JsonSerializer.Serialize(paymentPayload, _jsonOptions));

            // 🔹 Отправляем запрос
            var response = await _httpClient.SendAsync(requestMessage, ct);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("📥 Ответ ЮKassa: {StatusCode} {Content}", response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ ЮKassa ошибка {StatusCode}: {Content}", response.StatusCode, responseContent);
                throw new InvalidOperationException($"ЮKassa вернула ошибку {(int)response.StatusCode}: {responseContent}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            return new PaymentResponse
            {
                PaymentId = GetStringProperty(json, "id"),
                ConfirmationUrl = GetStringProperty(json, "confirmation", "confirmation_url"),
                Status = GetStringProperty(json, "status")
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "🌐 HTTP ошибка к ЮKassa");
            throw new InvalidOperationException($"Не удалось соединиться с ЮKassa: {ex.Message}", ex);
        }
    }

    public async Task<PaymentResponse?> GetPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("PaymentId не может быть пустым", nameof(paymentId));

        try
        {
            var response = await _httpClient.GetAsync($"payments/{paymentId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return new PaymentResponse
            {
                PaymentId = GetStringProperty(json, "id"),
                Status = GetStringProperty(json, "status"),
                ConfirmationUrl = GetStringPropertyOrNull(json, "confirmation", "confirmation_url") ?? string.Empty
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ошибка получения платежа {PaymentId}", paymentId);
            throw;
        }
    }

    public async Task<WebhookResult> ProcessWebhookAsync(string jsonBody, string? signature = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jsonBody))
            throw new ArgumentException("Тело вебхука не может быть пустым", nameof(jsonBody));

        if (!string.IsNullOrEmpty(_webhookSecret) && !string.IsNullOrEmpty(signature))
        {
            if (!VerifyWebhookSignature(jsonBody, signature, _webhookSecret))
            {
                _logger.LogWarning("Неверная подпись вебхука: {Signature}", signature);
                throw new SecurityException("Invalid webhook signature");
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            return new WebhookResult
            {
                RawJson = jsonBody,
                Event = root.TryGetProperty("event", out var e) ? e.GetString() : null,
                PaymentId = GetNestedStringProperty(root, "object", "id"),
                Status = GetNestedStringProperty(root, "object", "status"),
                Amount = GetNestedDecimalProperty(root, "object", "amount", "value"),
                Currency = GetNestedStringProperty(root, "object", "amount", "currency"),
                OrderId = GetNestedStringProperty(root, "object", "metadata", "order_id")
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Ошибка парсинга вебхука");
            throw new InvalidOperationException("Некорректный формат вебхука", ex);
        }
    }

    public bool VerifyWebhookSignature(string jsonBody, string signature, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(jsonBody) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(webhookSecret))
            return false;

        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonBody));
            var computedSignature = Convert.ToBase64String(hash);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(computedSignature));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки подписи вебхука");
            return false;
        }
    }

    #region Helper Methods
    private static string TruncateDescription(string description, int maxLength = 128) =>
        string.IsNullOrWhiteSpace(description) ? "Оплата заказа" :
        description.Length > maxLength ? description[..maxLength] : description;

    private static string GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        var current = element;
        foreach (var prop in propertyNames)
        {
            if (current.TryGetProperty(prop, out var next)) current = next;
            else throw new KeyNotFoundException($"Property '{string.Join(".", propertyNames)}' not found");
        }
        return current.GetString() ?? string.Empty;
    }

    private static string? GetStringPropertyOrNull(JsonElement element, params string[] propertyNames)
    {
        try { return GetStringProperty(element, propertyNames); }
        catch { return null; }
    }

    private static DateTime? GetDateTimePropertyOrNull(JsonElement element, params string[] propertyNames)
    {
        try
        {
            var value = GetStringProperty(element, propertyNames);
            return DateTime.TryParse(value, out var dt) ? dt : null;
        }
        catch { return null; }
    }

    private static string GetNestedStringProperty(JsonElement root, params string[] propertyNames)
    {
        var current = root;
        foreach (var prop in propertyNames)
        {
            if (current.TryGetProperty(prop, out var next)) current = next;
            else return string.Empty;
        }
        return current.GetString() ?? string.Empty;
    }

    private static decimal GetNestedDecimalProperty(JsonElement root, params string[] propertyNames)
    {
        var current = root;
        foreach (var prop in propertyNames)
        {
            if (current.TryGetProperty(prop, out var next)) current = next;
            else return 0;
        }
        return current.GetDecimal();
    }
    #endregion
}