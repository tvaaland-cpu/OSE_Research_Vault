using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IMemoPublishService
{
    bool SupportsPdf { get; }
    Task<MemoPublishResult> PublishAsync(MemoPublishRequest request, CancellationToken cancellationToken = default);
}
