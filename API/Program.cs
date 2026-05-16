// 📁 API/Program.cs
using API.Models;
using API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 🔹 НАСТРОЙКА КОНФИГУРАЦИИ
// ==========================================
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// ==========================================
// 🔹 РЕГИСТРАЦИЯ СЕРВИСОВ
// ==========================================

// 🔹 1. Регистрируем сервис проверки подключения к БД
builder.Services.AddSingleton<IDatabaseHealthCheck, DatabaseHealthCheck>();

// 🔹 2. Подключение к БД с динамической строкой
builder.Services.AddDbContext<DiplomContext>((serviceProvider, options) =>
{
    var healthCheck = serviceProvider.GetRequiredService<IDatabaseHealthCheck>();
    var connStr = healthCheck.GetWorkingConnectionString();

    if (string.IsNullOrEmpty(connStr))
    {
        throw new InvalidOperationException(
            "❌ Не удалось подключиться к базе данных. Проверьте настройки ConnectionStrings в appsettings.json");
    }

    options.UseNpgsql(connStr, b =>
        b.MigrationsAssembly(typeof(DiplomContext).Assembly.FullName));
});

// 🔹 3. Регистрация контроллеров с настройками JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// 🔹 4. Регистрация сервисов приложения
builder.Services.AddScoped<IEmailService, EmailService>();

// 🔹 5. Настройка JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey не настроен!");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// 🔹 6. Регистрация Swagger с поддержкой авторизации
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Кафе API",
        Version = "v1",
        Description = "API для системы управления кафе"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите токен: Bearer {your_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 🔹 7. Настройка CORS
var corsOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
                  ?? new[] { "http://localhost:7001", "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClients", builder =>
    {
        builder
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// 🔹 8. 🔹 🔹 🔹 RATE LIMITING (ВСТРОЕННЫЙ В .NET 9 — ПАКЕТ НЕ НУЖЕН!)
builder.Services.AddRateLimiter(options =>
{
    // 🔹 Глобальный лимит: 100 запросов в минуту с одного IP
    options.AddFixedWindowLimiter("fixed", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    // 🔹 Более строгий лимит для авторизации (защита от подбора паролей)
    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 5;  // Всего 5 попыток входа в минуту!
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 🔹 Применяем лимиты по IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // 🔹 Строгие лимиты для эндпоинтов авторизации
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1)
                });
        }

        // 🔹 Обычные лимиты для остальных эндпоинтов
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

// 🔹 9. Фоновый мониторинг БД
builder.Services.AddHostedService<DatabaseMonitorService>();

// 🔹 10. Ограничения Kestrel (защита от перегрузки)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxConcurrentUpgradedConnections = 10;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    options.Limits.MinRequestBodyDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
    options.Limits.MinResponseDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
});

// ==========================================
// 🔹 СБОРКА ПРИЛОЖЕНИЯ
// ==========================================

var app = builder.Build();

// ==========================================
// 🔹 КОНФИГУРАЦИЯ MIDDLEWARE PIPELINE
// ==========================================

// 🔹 Порядок middleware ВАЖЕН!

// 1. Swagger (только в разработке)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Кафе API v1");
        c.RoutePrefix = "swagger";
    });
}

// 2. Перенаправление корня на Swagger
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/swagger");
        return;
    }
    await next();
});

// 3. CORS — ДО UseAuthorization!
app.UseCors("AllowClients");

// 4. HTTPS редирект
app.UseHttpsRedirection();

// 🔹 5. 🔹 🔹 🔹 RATE LIMITING — ДО аутентификации!
app.UseRateLimiter();

// 6. Аутентификация — ДО Authorization!
app.UseAuthentication();

// 7. Авторизация
app.UseAuthorization();

// 🔹 8. Middleware для проверки подключения к БД
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/health"))
    {
        var healthCheck = context.RequestServices.GetService<IDatabaseHealthCheck>();
        if (healthCheck != null && !await healthCheck.CheckDatabaseConnectionAsync())
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Database unavailable",
                message = "Не удалось подключиться к базе данных. Попробуйте позже."
            });
            return;
        }
    }
    await next();
});

// 9. Маппинг контроллеров
app.MapControllers();

// 🔹 10. Endpoint для проверки здоровья БД
app.MapGet("/health/db", async (IDatabaseHealthCheck healthCheck) =>
{
    var isHealthy = await healthCheck.CheckDatabaseConnectionAsync();
    var currentConn = healthCheck.GetCurrentConnectionString();

    return Results.Json(new
    {
        status = isHealthy ? "healthy" : "unhealthy",
        connectionString = isHealthy ? "Connected" : "Not connected",
        host = ExtractHost(currentConn),
        timestamp = DateTime.UtcNow
    });
});

// 🔹 Простой health check
app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

// ==========================================
// 🔹 ЗАПУСК
// ==========================================

app.Run();

// ==========================================
// 🔹 ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
// ==========================================

static string ExtractHost(string? connStr)
{
    if (string.IsNullOrEmpty(connStr)) return "unknown";

    var hostParam = connStr.Split(';')
        .FirstOrDefault(p => p.StartsWith("Host=", StringComparison.OrdinalIgnoreCase));
    return hostParam?.Substring(5) ?? "unknown";
}