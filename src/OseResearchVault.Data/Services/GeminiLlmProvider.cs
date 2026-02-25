#if GEMINI_PROVIDER
using System.Text;
using System.Text.Json;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class GeminiLlmProvider(HttpClient httpClient, ISecretStore secretStore) : ILLMProvider
{
    public string ProviderName => "google";

    public async Task<string> GenerateAsync(string prompt, string contextDocsText, LlmGenerationSettings settings, CancellationToken cancellationToken = default)
    {
        var apiKey = await secretStore.GetSecretAsync(settings.ApiKeySecretName, cancellationToken)
            ?? throw new InvalidOperationException($"Missing API key secret: {settings.ApiKeySecretName}");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{settings.Model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        using var response = await httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(new
        {
            generationConfig = new { temperature = settings.Temperature, maxOutputTokens = settings.MaxTokens },
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt },
                        new { text = $"CONTEXT:\n{contextDocsText}" }
                    }
                }
            }
        }), Encoding.UTF8, "application/json"), cancellationToken);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return payload.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
#endif
