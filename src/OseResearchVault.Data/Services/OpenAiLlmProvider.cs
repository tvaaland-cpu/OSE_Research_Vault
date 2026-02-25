#if OPENAI_PROVIDER
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class OpenAiLlmProvider(HttpClient httpClient, ISecretStore secretStore) : ILLMProvider
{
    public string ProviderName => "openai";

    public async Task<string> GenerateAsync(string prompt, string contextDocsText, LlmGenerationSettings settings, CancellationToken cancellationToken = default)
    {
        var apiKey = await secretStore.GetSecretAsync(settings.ApiKeySecretName, cancellationToken)
            ?? throw new InvalidOperationException($"Missing API key secret: {settings.ApiKeySecretName}");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", new StringContent(JsonSerializer.Serialize(new
        {
            model = settings.Model,
            temperature = settings.Temperature,
            max_tokens = settings.MaxTokens,
            messages = new[]
            {
                new { role = "system", content = "Use only provided context when possible." },
                new { role = "user", content = $"{prompt}\n\nCONTEXT:\n{contextDocsText}" }
            }
        }), Encoding.UTF8, "application/json"), cancellationToken);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return payload.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
#endif
