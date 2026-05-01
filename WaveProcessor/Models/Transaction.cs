using System.Text.Json;

namespace WaveProcessor.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? FromUserId { get; set; }
    public Guid? ToUserId { get; set; }
    public string? FromPhone { get; set; }
    public string? ToPhone { get; set; }
    public decimal Amount { get; set; }
    public decimal? Fee { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public Guid? AgentId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public JsonDocument? ExtraData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
