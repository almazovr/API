using System;
using System.Collections.Generic;

namespace API.Models

{
    public partial class StockMovement
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }          
        public int QuantityChange { get; set; } 
        public string MovementType { get; set; } = null!;  
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Product Product { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
