using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class RedactionServiceTests
{
    [Fact]
    public void Redact_MasksEmailPhonePathAndSecrets()
    {
        var service = new RegexRedactionService();
        var input = "Email jane@example.com phone 555-123-4567 key AKIAABCDEFGHIJKLMNOP path C:\\Users\\Jane\\secrets.txt";

        var result = service.Redact(input, new RedactionOptions());

        Assert.Contains("[REDACTED:EMAIL]", result.RedactedText);
        Assert.Contains("[REDACTED:PHONE]", result.RedactedText);
        Assert.Contains("[REDACTED:PATH]", result.RedactedText);
        Assert.Contains("[REDACTED:SECRET]", result.RedactedText);
        Assert.True(result.Hits.Count >= 4);
    }

    [Fact]
    public void Redact_IsDeterministic()
    {
        var service = new RegexRedactionService();
        var options = new RedactionOptions();
        var input = "token=ghp_abcdefghijklmnopqrstuvwxyz123456";

        var first = service.Redact(input, options);
        var second = service.Redact(input, options);

        Assert.Equal(first.RedactedText, second.RedactedText);
        Assert.Equal(first.Hits.Count, second.Hits.Count);
    }
}
