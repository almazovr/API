namespace API.DTOs
{
    public class CashierReportDto
    {
        public int UserId { get; set; }
        public string CashierName { get; set; } = null!;
        public DateTime WorkDate { get; set; }  // Дата (без времени)
        public int OrdersCount { get; set; }    // Количество заказов
        public decimal TotalAmount { get; set; } // Сумма заказов
    }

    public class CashierReportRequest
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
    }
}
