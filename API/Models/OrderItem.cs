using System;
using System.Collections.Generic;

namespace API.Models;

public partial class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int? ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? ProductBrand { get; set; }

    public int Quantity { get; set; }

    public decimal PriceAtTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product? Product { get; set; }
}
