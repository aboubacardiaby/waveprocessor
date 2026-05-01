using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WaveProcessor.Data;
using WaveProcessor.Models;

namespace WaveProcessor.Services;

public class TransactionProcessorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WaveApiService _waveApi;
    private readonly ILogger<TransactionProcessorWorker> _logger;
    private readonly TimeSpan _pollInterval;

    public TransactionProcessorWorker(
        IServiceScopeFactory scopeFactory,
        WaveApiService waveApi,
        ILogger<TransactionProcessorWorker> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _waveApi = waveApi;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(config.GetValue("Processor:PollIntervalSeconds", 30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Wave transaction processor started. Poll interval: {Interval}s", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingTransactionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error during transaction processing cycle.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingTransactionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.Transactions
            .Where(t => t.Type == "wave_transfer" && t.Status == "pending")
            .OrderBy(t => t.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        _logger.LogInformation("Found {Count} pending wave_transfer transaction(s).", pending.Count);

        foreach (var transaction in pending)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ProcessTransactionAsync(db, transaction, cancellationToken);
        }
    }

    private async Task ProcessTransactionAsync(AppDbContext db, Transaction transaction, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing transaction {Ref} (id={Id})", transaction.TransactionRef, transaction.Id);

        var (toPhone, sendAmount, recvCurrency) = ExtractWaveFields(transaction);

        if (toPhone is null || recvCurrency is null)
        {
            _logger.LogError("Transaction {Ref} is missing required wave fields — marking failed.", transaction.TransactionRef);
            await UpdateStatusAsync(db, transaction, "failed", null, cancellationToken);
            return;
        }

        try
        {
            var result = await _waveApi.SendB2CPaymentAsync(
                toPhone: toPhone,
                amount: sendAmount ?? transaction.Amount,
                currency: recvCurrency,
                transactionRef: transaction.TransactionRef,
                description: transaction.Description ?? string.Empty,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Wave payment accepted. Ref={Ref} WaveRef={WaveRef} Simulated={Simulated}",
                transaction.TransactionRef, result.WaveRef, result.Simulated);

            await UpdateStatusAsync(db, transaction, "completed", result.WaveRef, cancellationToken);
        }
        catch (WaveApiException ex)
        {
            _logger.LogError(ex, "Wave API error for transaction {Ref} (status={StatusCode})",
                transaction.TransactionRef, ex.StatusCode);

            // Only mark failed on non-retryable errors; keep pending for transient failures
            if (ex.StatusCode is 400 or 422)
                await UpdateStatusAsync(db, transaction, "failed", null, cancellationToken);
        }
    }

    private async Task UpdateStatusAsync(
        AppDbContext db,
        Transaction transaction,
        string status,
        string? waveRef,
        CancellationToken cancellationToken)
    {
        transaction.Status = status;

        if (status == "completed")
            transaction.CompletedAt = DateTime.UtcNow;

        if (waveRef is not null)
        {
            var extra = transaction.ExtraData is not null
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(transaction.ExtraData.RootElement.GetRawText()) ?? []
                : [];

            extra["wave_ref"] = JsonSerializer.SerializeToElement(waveRef);
            transaction.ExtraData = JsonDocument.Parse(JsonSerializer.Serialize(extra));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static (string? toPhone, decimal? sendAmount, string? recvCurrency) ExtractWaveFields(Transaction transaction)
    {
        var toPhone = transaction.ToPhone;
        decimal? sendAmount = null;
        string? recvCurrency = null;

        if (transaction.ExtraData is not null)
        {
            var root = transaction.ExtraData.RootElement;

            if (root.TryGetProperty("wave_phone", out var phoneEl))
                toPhone = phoneEl.GetString();

            if (root.TryGetProperty("net_send_amount", out var amountEl) && amountEl.TryGetDecimal(out var amt))
                sendAmount = amt;

            if (root.TryGetProperty("recv_currency", out var currEl))
                recvCurrency = currEl.GetString();
        }

        return (toPhone, sendAmount, recvCurrency);
    }
}
