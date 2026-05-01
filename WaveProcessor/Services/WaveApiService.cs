using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WaveProcessor.Services;

public class WavePaymentResult
{
    public string WaveRef { get; set; } = string.Empty;
    public string WaveTransactionId { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public decimal TotalDeducted { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsInternational { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool Simulated { get; set; }
}

public class WaveApiException : Exception
{
    public int? StatusCode { get; }
    public WaveApiException(string message, int? statusCode = null) : base(message) => StatusCode = statusCode;
}

public class WaveApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WaveApiService> _logger;

    // Auto-login credentials (preferred — token is self-renewing)
    private readonly string? _phoneNumber;
    private readonly string? _pin;

    // Pre-issued static bearer token (fallback when credentials are not configured)
    private readonly string? _staticToken;

    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public bool IsSimulationMode =>
        string.IsNullOrWhiteSpace(_phoneNumber) &&
        string.IsNullOrWhiteSpace(_staticToken);

    public WaveApiService(HttpClient httpClient, ILogger<WaveApiService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _phoneNumber = config["Wave:PhoneNumber"];
        _pin = config["Wave:Pin"];
        _staticToken = config["Wave:ApiKey"];
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        // If credentials are configured, use auto-login with caching
        if (!string.IsNullOrWhiteSpace(_phoneNumber))
        {
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
                return _cachedToken;

            await _tokenLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
                    return _cachedToken;

                _logger.LogInformation("Authenticating with Wave API...");

                using var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/login");
                req.Content = JsonContent.Create(new { phoneNumber = _phoneNumber, pin = _pin });

                HttpResponseMessage res;
                try { res = await _httpClient.SendAsync(req, cancellationToken); }
                catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
                {
                    throw new WaveApiException($"Wave API auth failed: {ex.Message}", 503);
                }

                if (!res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync(cancellationToken);
                    throw new WaveApiException($"Wave login failed ({(int)res.StatusCode}): {body}", (int)res.StatusCode);
                }

                var login = await res.Content.ReadFromJsonAsync<WaveLoginResponse>(cancellationToken: cancellationToken);
                if (login?.Token is null)
                    throw new WaveApiException("Wave API login response missing token.");

                _cachedToken = login.Token;
                _tokenExpiresAt = DateTime.UtcNow.AddDays(7);
                _logger.LogInformation("Wave API authentication successful.");
                return _cachedToken;
            }
            finally { _tokenLock.Release(); }
        }

        // Fall back to pre-issued static token
        return _staticToken!;
    }

    public async Task<WavePaymentResult> SendTransferAsync(
        string receiverPhone,
        decimal amount,
        string transactionRef,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (IsSimulationMode)
        {
            var simRef = $"WV{DateTime.UtcNow:yyyyMMdd}{transactionRef[..Math.Min(6, transactionRef.Length)].ToUpper()}";
            _logger.LogWarning("Wave credentials not configured — simulation mode. Ref={SimRef}", simRef);
            return new WavePaymentResult
            {
                WaveRef = simRef,
                WaveTransactionId = Guid.NewGuid().ToString(),
                Status = "Completed",
                Simulated = true
            };
        }

        var token = await GetTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/transfers/send");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { receiverPhone, amount, note = note ?? string.Empty });

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

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Clear cached token so next attempt re-authenticates
            _cachedToken = null;
            throw new WaveApiException("Wave API unauthorized — token may have expired.", 401);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Wave API error {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new WaveApiException($"Wave API returned {(int)response.StatusCode}: {body}", (int)response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<WaveTransferResponse>(cancellationToken: cancellationToken);
        if (result?.Id is null)
            throw new WaveApiException("Wave API response missing transaction id.");

        return new WavePaymentResult
        {
            WaveRef = result.Reference ?? result.Id,
            WaveTransactionId = result.Id,
            Fee = result.Fee,
            TotalDeducted = result.TotalDeducted,
            Currency = result.Currency ?? string.Empty,
            IsInternational = result.IsInternational,
            Status = result.Status ?? "Completed",
            Simulated = false
        };
    }

    private sealed class WaveLoginResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    private sealed class WaveTransferResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("fee")]
        public decimal Fee { get; set; }

        [JsonPropertyName("totalDeducted")]
        public decimal TotalDeducted { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("isInternational")]
        public bool IsInternational { get; set; }
    }
}
