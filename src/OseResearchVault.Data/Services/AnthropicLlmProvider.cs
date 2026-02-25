#if ANTHROPIC_PROVIDER
using System.Text;
using System.Text.Json;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class AnthropicLlmProvider(HttpClient httpClient, ISecretStore secretStore) : ILLMProvider
{
    public string ProviderName => "anthropic";

    public async Task<string> GenerateAsync(string prompt, string contextDocsText, LlmGenerationSettings settings, CancellationToken cancellationToken = default)
    {
        var apiKey = await secretStore.GetSecretAsync(settings.ApiKeySecretName, cancellationToken)
            ?? throw new InvalidOperationException($"Missing API key secret: {settings.ApiKeySecretName}");

        httpClient.DefaultRequestHeaders.Remove("x-api-key");
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Remove("anthropic-version");
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        using var response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", new StringContent(JsonSerializer.Serialize(new
        {
            model = settings.Model,
            max_tokens = settings.MaxTokens,
            temperature = settings.Temperature,
            messages = new[] { new { role = "user", content = $"{prompt}\n\nCONTEXT:\n{contextDocsText}" } }
        }), Encoding.UTF8, "application/json"), cancellationToken);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return payload.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
#endif
