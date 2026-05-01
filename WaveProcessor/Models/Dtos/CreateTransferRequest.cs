using System.ComponentModel.DataAnnotations;

namespace WaveProcessor.Models.Dtos;

public class CreateTransferRequest
{
    [Required]
    public string ToPhone { get; set; } = string.Empty;

    [Required]
    [Range(1, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional — Wave determines currency from the sender's wallet automatically.
    /// Stored for reference only.
    /// </summary>
    [StringLength(10)]
    public string? Currency { get; set; }

    [StringLength(255)]
    public string? Note { get; set; }
}
