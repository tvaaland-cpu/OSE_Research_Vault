using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.Data.Services;

public sealed class FileSecretStore : ISecretStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var secrets = await LoadSecretsAsync(cancellationToken);
        if (!secrets.TryGetValue(name, out var encrypted))
        {
            return null;
        }

        return Decrypt(encrypted);
    }

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        var secrets = await LoadSecretsAsync(cancellationToken);
        secrets[name] = Encrypt(value);

        Directory.CreateDirectory(AppPaths.DefaultRootDirectory);
        await using var stream = File.Create(AppPaths.SecretsFilePath);
        await JsonSerializer.SerializeAsync(stream, secrets, SerializerOptions, cancellationToken);
    }

    private static async Task<Dictionary<string, string>> LoadSecretsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.SecretsFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(AppPaths.SecretsFilePath);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken) ?? [];
    }

    private static string Encrypt(string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);

        if (OperatingSystem.IsWindows())
        {
            var protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedData);
        }

        return Convert.ToBase64String(data);
    }

    private static string Decrypt(string encrypted)
    {
        var bytes = Convert.FromBase64String(encrypted);

        if (OperatingSystem.IsWindows())
        {
            var unprotected = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotected);
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
