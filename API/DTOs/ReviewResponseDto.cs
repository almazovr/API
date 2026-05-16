// 📁 API/DTOs/ReviewDto.cs
using System;

namespace API.DTOs;

public class ReviewDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ReviewCreateDto
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReviewUpdateDto
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}