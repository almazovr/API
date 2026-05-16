// 📁 API/Controllers/ReportController.cs
using API.DTOs;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Text;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReportController : ControllerBase
{
    private readonly DiplomContext _context;
    private const int ROLE_CASHIER = 7;
    private const int STATUS_ISSUED = 11;

    public ReportController(DiplomContext context) => _context = context;

    [HttpGet("cashiers")]
    public async Task<ActionResult<IEnumerable<CashierReportDto>>> GetCashierReport(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] int statusId,
        [FromHeader] int? userId)
    {
        if (!userId.HasValue)
            return StatusCode((int)HttpStatusCode.Unauthorized, new { message = "Требуется авторизация: передайте userId в заголовке" });

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
            return StatusCode((int)HttpStatusCode.NotFound, new { message = $"Пользователь с ID={userId.Value} не найден" });

        var allowedRoleIds = new[] { 1, 2 };
        if (!allowedRoleIds.Contains(user.RoleId ?? 0))
            return StatusCode((int)HttpStatusCode.Forbidden, new { message = $"Доступ запрещён: роль {user.RoleId} не имеет прав на просмотр отчётов" });

        if (dateFrom > dateTo)
            return BadRequest(new { message = "Дата начала не может быть позже даты окончания" });

        var from = DateTime.SpecifyKind(dateFrom.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(dateTo.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        // 🔹 🔹 🔹 ИСПРАВЛЕНО: Фильтр по CashierId, а не по User.RoleId
        var rawData = await _context.Orders
            .Include(o => o.Cashier)  // 🔹 Загружаем данные кассира
            .Where(o => o.CashierId.HasValue              // 🔹 Только заказы с назначенным кассиром
                     && o.Cashier != null                 // 🔹 Защита от null
                     && o.Cashier.RoleId == ROLE_CASHIER  // 🔹 Только кассиры (RoleId=7)
                     && o.CreatedAt >= from
                     && o.CreatedAt <= to
                     && o.StatusId == statusId)           // 🔹 Только статус "Выдан"
            .GroupBy(o => new { o.CashierId, Date = o.CreatedAt.Value.Date })  // 🔹 Группировка по кассиру
            .Select(g => new
            {
                UserId = g.Key.CashierId,  // 🔹 Возвращаем ID кассира
                // 🔹 Безопасное получение логина кассира
                UserLogin = g.First().Cashier != null ? g.First().Cashier.Login : null,
                WorkDate = g.Key.Date,
                OrdersCount = g.Count(),
                TotalAmount = g.Sum(o => o.TotalAmount)
            })
            .ToListAsync();

        var report = rawData.Select(r => new CashierReportDto
        {
            UserId = r.UserId ?? 0,
            // 🔹 Формируем имя кассира
            CashierName = r.UserLogin != null ? r.UserLogin : $"ID:{r.UserId}",
            WorkDate = r.WorkDate,
            OrdersCount = r.OrdersCount,
            TotalAmount = r.TotalAmount
        })
        .OrderBy(r => r.WorkDate)
        .ThenBy(r => r.CashierName)
        .ToList();

        return Ok(report);
    }

    [HttpGet("cashiers/export")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCashierReportCsv(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] int statusId,
        [FromHeader] int? userId)
    {
        if (!userId.HasValue)
            return StatusCode((int)HttpStatusCode.Unauthorized, new { message = "Требуется авторизация: передайте userId в заголовке" });

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
            return StatusCode((int)HttpStatusCode.NotFound, new { message = $"Пользователь с ID={userId.Value} не найден" });

        var allowedRoleIds = new[] { 1, 2 };
        if (!allowedRoleIds.Contains(user.RoleId ?? 0))
            return StatusCode((int)HttpStatusCode.Forbidden, new { message = $"Доступ запрещён: роль {user.RoleId} не имеет прав на экспорт отчётов" });

        if (dateFrom > dateTo)
            return BadRequest(new { message = "Дата начала не может быть позже даты окончания" });

        var report = await GetCashierReport(dateFrom, dateTo, statusId, userId);

        if (report.Value == null || !report.Value.Any())
        {
            var emptyCsv = "Дата;Кассир;ID кассира;Количество заказов;Сумма (₽);Сумма (число)\n" +
                          $";Нет данных за период {dateFrom:dd.MM.yyyy}–{dateTo:dd.MM.yyyy} (статус: {statusId});;;;";

            var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var emptyBytes = Encoding.UTF8.GetBytes(emptyCsv);
            var bytesWithBom = new byte[utf8Bom.Length + emptyBytes.Length];
            Array.Copy(utf8Bom, bytesWithBom, utf8Bom.Length);
            Array.Copy(emptyBytes, 0, bytesWithBom, utf8Bom.Length, emptyBytes.Length);

            var emptyFileName = $"cashier_report_empty_{dateFrom:yyyy-MM-dd}_to_{dateTo:yyyy-MM-dd}.csv";
            return File(bytesWithBom, "text/csv; charset=utf-8", emptyFileName);
        }

        var csv = BuildCashierReportCsv(report.Value, dateFrom, dateTo);

        var utf8BomFinal = new byte[] { 0xEF, 0xBB, 0xBF };
        var csvBytes = Encoding.UTF8.GetBytes(csv);
        var bytesWithBomFinal = new byte[utf8BomFinal.Length + csvBytes.Length];

        Array.Copy(utf8BomFinal, bytesWithBomFinal, utf8BomFinal.Length);
        Array.Copy(csvBytes, 0, bytesWithBomFinal, utf8BomFinal.Length, csvBytes.Length);

        var fileName = $"cashier_report_{dateFrom:yyyy-MM-dd}_to_{dateTo:yyyy-MM-dd}.csv";

        return File(bytesWithBomFinal, "text/csv; charset=utf-8", fileName);
    }

    private string BuildCashierReportCsv(IEnumerable<CashierReportDto> data, DateTime from, DateTime to)
    {
        var culture = new CultureInfo("ru-RU");
        var sb = new StringBuilder();

        sb.AppendLine("Дата;Кассир;ID кассира;Количество заказов;Сумма (₽);Сумма (число)");

        foreach (var row in data)
        {
            sb.AppendLine($"{row.WorkDate:dd.MM.yyyy};" +
                         $"\"{EscapeCsv(row.CashierName)}\";" +
                         $"{row.UserId};" +
                         $"{row.OrdersCount};" +
                         $"{row.TotalAmount.ToString("N2", culture)};" +
                         $"{row.TotalAmount.ToString("F2", culture)}");
        }

        var totalOrders = data.Sum(r => r.OrdersCount);
        var totalAmount = data.Sum(r => r.TotalAmount);
        sb.AppendLine($";ИТОГО за период {from:dd.MM.yyyy}–{to:dd.MM.yyyy};;{totalOrders};{totalAmount.ToString("N2", culture)};{totalAmount.ToString("F2", culture)}");

        return sb.ToString();
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"").Replace(";", ",");
    }
}