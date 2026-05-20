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

// ==========================================
// 🔹 1. СОЗДАНИЕ BUILDER
// ==========================================
var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 🔹 2. НАСТРОЙКА КОНФИГУРАЦИИ (ДО Build!)
// ==========================================
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
builder.Services.AddHttpClient<YooKassaService>();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddHttpClient<IYooKassaService, YooKassaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30); 
});

// ==========================================
// 🔹 3. РЕГИСТРАЦИЯ ВСЕХ СЕРВИСОВ (ДО Build!)
// ==========================================

// 🔹 Swagger & Endpoints
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
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 🔹 Контроллеры + настройки JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// 🔹 Проверка БД
builder.Services.AddSingleton<IDatabaseHealthCheck, DatabaseHealthCheck>();

// 🔹 DbContext с динамической строкой подключения
builder.Services.AddDbContext<DiplomContext>((serviceProvider, options) =>
{
    var healthCheck = serviceProvider.GetRequiredService<IDatabaseHealthCheck>();
    var connStr = healthCheck.GetWorkingConnectionString();

    if (string.IsNullOrEmpty(connStr))
        throw new InvalidOperationException("❌ Не удалось подключиться к БД. Проверьте ConnectionStrings в appsettings.json");

    options.UseNpgsql(connStr, b => b.MigrationsAssembly(typeof(DiplomContext).Assembly.FullName));
});

// 🔹 Сервисы приложения
builder.Services.AddScoped<IEmailService, EmailService>();

// 🔹 JWT Authentication
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

// 🔹 CORS
var corsOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
                  ?? new[] { "http://localhost:7001", "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClients", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 🔹 Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) });
    });
});

// 🔹 Фоновые сервисы
builder.Services.AddHostedService<DatabaseMonitorService>();

// 🔹 Kestrel limits
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxConcurrentUpgradedConnections = 10;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
    options.Limits.MinRequestBodyDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
    options.Limits.MinResponseDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
});

// ==========================================
// 🔹 4. ПОСТРОЕНИЕ ПРИЛОЖЕНИЯ (ТЕПЕРЬ МОЖНО!)
// ==========================================
var app = builder.Build();

// ==========================================
// 🔹 5. НАСТРОЙКА MIDDLEWARE (ПОСЛЕ Build!)
// ==========================================

// 🔹 Swagger (только в Development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Кафе API v1");
        c.RoutePrefix = "swagger";
    });
}

// 🔹 Редирект с "/" на "/swagger"
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/swagger");
        return;
    }
    await next();
});

// 🔹 CORS — ДО UseAuthorization!
app.UseCors("AllowClients");

// 🔹 HTTPS редирект
app.UseHttpsRedirection();

// 🔹 Rate Limiting — ДО аутентификации!
app.UseRateLimiter();

// 🔹 Аутентификация и авторизация
app.UseAuthentication();
app.UseAuthorization();

// 🔹 Middleware проверки БД для /api/* (кроме /api/health)
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

// ==========================================
// 🔹 6. МАППИНГ ЭНДПОИНТОВ
// ==========================================
app.MapControllers();

// 🔹 Health check БД
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
// 🔹 7. ЗАПУСК
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