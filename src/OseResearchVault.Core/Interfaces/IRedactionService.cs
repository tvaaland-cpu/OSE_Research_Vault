using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IRedactionService
{
    RedactionResult Redact(string text, RedactionOptions options);
}
