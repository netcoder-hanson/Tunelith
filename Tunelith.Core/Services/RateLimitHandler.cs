using System.Collections.Concurrent;

namespace Tunelith.Core.Services;

public class RateLimitHandler
{
    private readonly ConcurrentQueue<DateTime> _spotifyRequestTimestamps = new();
    private readonly ConcurrentQueue<DateTime> _geminiRequestTimestamps = new();

    private const int SpotifyMaxRequestsPerSecond = 30;
    private const int GeminiMaxRequestsPerMinute = 60;
    private const int MaxRetries = 5;
    private const double BaseDelayMs = 1000;

    public async Task WaitForSpotifySlot(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            while (_spotifyRequestTimestamps.TryPeek(out var oldest) &&
                   (now - oldest).TotalSeconds > 1)
            {
                _spotifyRequestTimestamps.TryDequeue(out _);
            }

            if (_spotifyRequestTimestamps.Count < SpotifyMaxRequestsPerSecond)
            {
                _spotifyRequestTimestamps.Enqueue(DateTime.UtcNow);
                return;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    public async Task WaitForGeminiSlot(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            while (_geminiRequestTimestamps.TryPeek(out var oldest) &&
                   (now - oldest).TotalMinutes > 1)
            {
                _geminiRequestTimestamps.TryDequeue(out _);
            }

            if (_geminiRequestTimestamps.Count < GeminiMaxRequestsPerMinute)
            {
                _geminiRequestTimestamps.Enqueue(DateTime.UtcNow);
                return;
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    public async Task<T> ExecuteWithBackoff<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<int, TimeSpan> getRetryDelay,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt == MaxRetries)
                    throw;

                var delay = getRetryDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Max retries exceeded");
    }

    public TimeSpan GetRetryDelayFromResponse(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
            return response.Headers.RetryAfter.Delta.Value;

        if (response.Headers.RetryAfter?.Date.HasValue == true)
        {
            var waitUntil = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
            if (waitUntil > TimeSpan.Zero)
                return waitUntil;
        }

        return TimeSpan.Zero;
    }

    public TimeSpan GetSpotifyRetryDelay(int attempt)
    {
        return TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, attempt));
    }

    public TimeSpan GetGeminiRetryDelay(int attempt)
    {
        return TimeSpan.FromSeconds(30 * Math.Pow(2, attempt));
    }
}
