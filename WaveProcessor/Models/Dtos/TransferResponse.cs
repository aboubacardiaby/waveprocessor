using System.Text.Json;

namespace WaveProcessor.Models.Dtos;

public class TransferResponse
{
    public Guid Id { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ToPhone { get; set; }
    public decimal Amount { get; set; }
    public decimal? Fee { get; set; }
    public decimal? TotalDeducted { get; set; }
    public string? Currency { get; set; }
    public bool? IsInternational { get; set; }
    public string? Description { get; set; }
    public string? WaveRef { get; set; }
    public string? WaveTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static TransferResponse From(Transaction tx)
    {
        string? waveRef = null;
        string? waveTransactionId = null;
        decimal? fee = tx.Fee;
        decimal? totalDeducted = null;
        bool? isInternational = null;

        if (tx.ExtraData is not null)
        {
            var root = tx.ExtraData.RootElement;

            if (root.TryGetProperty("wave_ref", out var refEl))
                waveRef = refEl.GetString();

            if (root.TryGetProperty("wave_transaction_id", out var txIdEl))
                waveTransactionId = txIdEl.GetString();

            if (root.TryGetProperty("wave_total_deducted", out var tdEl) && tdEl.TryGetDecimal(out var td))
                totalDeducted = td;

            if (root.TryGetProperty("wave_is_international", out var intlEl))
                isInternational = intlEl.GetBoolean();
        }

        return new TransferResponse
        {
            Id = tx.Id,
            TransactionRef = tx.TransactionRef,
            Status = tx.Status,
            ToPhone = tx.ToPhone,
            Amount = tx.Amount,
            Fee = fee,
            TotalDeducted = totalDeducted,
            Currency = tx.Currency,
            IsInternational = isInternational,
            Description = tx.Description,
            WaveRef = waveRef,
            WaveTransactionId = waveTransactionId,
            CreatedAt = tx.CreatedAt,
            CompletedAt = tx.CompletedAt
        };
    }
}
