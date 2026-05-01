using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaveProcessor.Data;
using WaveProcessor.Models;
using WaveProcessor.Models.Dtos;

namespace WaveProcessor.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly AppDbContext _db;

    public TransfersController(AppDbContext db) => _db = db;

    /// <summary>
    /// Submit a money transfer to an individual Wave account.
    /// The background worker picks it up and dispatches it to the Wave API within the poll interval.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionRef = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            Type = "wave_transfer",
            Status = "pending",
            ToPhone = request.ToPhone,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetTransfer),
            new { id = transaction.Id },
            TransferResponse.From(transaction));
    }

    /// <summary>
    /// Get transfer status by internal ID.
    /// Poll this after creating a transfer to check when it reaches 'completed' or 'failed'.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTransfer(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (transaction is null)
            return NotFound(new { error = "Transfer not found." });

        return Ok(TransferResponse.From(transaction));
    }

    /// <summary>
    /// Get transfer status by transaction reference (e.g. TXN-20260501120000-AB12CD34).
    /// </summary>
    [HttpGet("ref/{transactionRef}")]
    public async Task<IActionResult> GetTransferByRef(string transactionRef, CancellationToken cancellationToken)
    {
        var transaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TransactionRef == transactionRef, cancellationToken);

        if (transaction is null)
            return NotFound(new { error = "Transfer not found." });

        return Ok(TransferResponse.From(transaction));
    }
}
