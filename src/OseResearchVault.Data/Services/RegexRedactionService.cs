using System.Text.RegularExpressions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed partial class RegexRedactionService : IRedactionService
{
    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(?:\+?\d{1,3}[\s\-.]?)?(?:\(?\d{3}\)?[\s\-.]?)\d{3}[\s\-.]?\d{4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b[A-Za-z]:\\(?:[^\\/:*?\""<>|\r\n]+\\)*[^\\/:*?\""<>|\r\n]*", RegexOptions.IgnoreCase)]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b", RegexOptions.IgnoreCase)]
    private static partial Regex AwsKeyRegex();

    [GeneratedRegex(@"\b(?:sk|pk)_(?:live|test)_[A-Za-z0-9]{16,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex StripeKeyRegex();

    [GeneratedRegex(@"\b(?:ghp|github_pat)_[A-Za-z0-9_]{20,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"\b(?:api[_-]?key|secret|token|password)\b\s*[:=]\s*['\"\"]?([A-Za-z0-9_\-/+=]{8,})", RegexOptions.IgnoreCase)]
    private static partial Regex GenericSecretRegex();

    public RedactionResult Redact(string text, RedactionOptions options)
    {
        var source = text ?? string.Empty;
        var hits = new List<RedactionHit>();

        if (options.MaskEmails)
        {
            source = ReplaceWithHits(source, EmailRegex(), "email", "[REDACTED:EMAIL]", hits);
        }

        if (options.MaskPhones)
        {
            source = ReplaceWithHits(source, PhoneRegex(), "phone", "[REDACTED:PHONE]", hits);
        }

        if (options.MaskPaths)
        {
            source = ReplaceWithHits(source, WindowsPathRegex(), "path", "[REDACTED:PATH]", hits);
        }

        if (options.MaskSecrets)
        {
            source = ReplaceWithHits(source, AwsKeyRegex(), "secret", "[REDACTED:SECRET]", hits);
            source = ReplaceWithHits(source, StripeKeyRegex(), "secret", "[REDACTED:SECRET]", hits);
            source = ReplaceWithHits(source, GitHubTokenRegex(), "secret", "[REDACTED:SECRET]", hits);
            source = ReplaceWithHits(source, GenericSecretRegex(), "secret", "[REDACTED:SECRET]", hits);
        }

        return new RedactionResult { RedactedText = source, Hits = hits };
    }

    private static string ReplaceWithHits(string input, Regex regex, string category, string replacement, ICollection<RedactionHit> hits)
    {
        return regex.Replace(input, match =>
        {
            hits.Add(new RedactionHit
            {
                Category = category,
                Value = match.Value,
                Replacement = replacement
            });

            return replacement;
        });
    }
}
