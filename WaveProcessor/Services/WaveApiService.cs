using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveProcessor.Services;

public class WavePaymentResult
{
    public string WaveRef { get; set; } = string.Empty;
    public bool Simulated { get; set; }
}

public class WaveApiException : Exception
{
    public int? StatusCode { get; }

    public WaveApiException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class WaveApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WaveApiService> _logger;
    private readonly string? _apiKey;

    public WaveApiService(HttpClient httpClient, ILogger<WaveApiService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["Wave:ApiKey"];
    }

    public async Task<WavePaymentResult> SendB2CPaymentAsync(
        string toPhone,
        decimal amount,
        string currency,
        string transactionRef,
        string description = "",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            var simRef = $"WAVE_SIM_{transactionRef[..Math.Min(12, transactionRef.Length)].ToUpper()}";
            _logger.LogWarning("Wave API key not configured — simulation mode. Ref={SimRef}", simRef);
            return new WavePaymentResult { WaveRef = simRef, Simulated = true };
        }

        var payload = new
        {
            currency,
            receive_amount = amount.ToString("F2"),
            mobile = toPhone,
            name = description,
            client_reference = transactionRef
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "b2c/payment");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WaveApiException("Wave API request timed out.", 503);
        }
        catch (HttpRequestException ex)
        {
            throw new WaveApiException($"Wave API network error: {ex.Message}", 503);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Wave API error {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new WaveApiException($"Wave API returned {(int)response.StatusCode}", (int)response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<WaveB2CResponse>(cancellationToken: cancellationToken);
        if (result?.Id is null)
            throw new WaveApiException("Wave API response missing transaction id.");

        return new WavePaymentResult { WaveRef = result.Id, Simulated = false };
    }

    private sealed class WaveB2CResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
