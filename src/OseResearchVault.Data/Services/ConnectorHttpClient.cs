using System.Net;
using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.Data.Services;

public sealed class ConnectorHttpClient(HttpClient httpClient) : IConnectorHttpClient
{
    private const int MaxAttempts = 3;

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(url, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(url, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                if (attempt == MaxAttempts || !IsRetriable(response.StatusCode))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
            }
        }

        throw lastException ?? new HttpRequestException($"Failed to fetch {url}");
    }

    private static bool IsRetriable(HttpStatusCode statusCode)
        => (int)statusCode >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests;
}
