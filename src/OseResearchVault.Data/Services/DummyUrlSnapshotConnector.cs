using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class DummyUrlSnapshotConnector(ISnapshotService snapshotService) : IConnector
{
    public string Id => "dummy-url-snapshot";
    public string DisplayName => "Dummy URL Snapshot";

    public async Task<ConnectorResult> RunAsync(ConnectorContext ctx, CancellationToken ct)
    {
        var targetUrl = ctx.Settings.TryGetValue("url", out var url) && !string.IsNullOrWhiteSpace(url)
            ? url
            : "https://example.com";

        var result = new ConnectorResult();
        try
        {
            var saved = await snapshotService.SaveUrlSnapshotAsync(targetUrl, ctx.WorkspaceId, ctx.CompanyId, "html", ct);
            result.SourcesCreated = 1;
            result.DocumentsCreated = 1;
            result.SourceIds.Add(saved.SourceId);
            result.DocumentIds.Add(saved.DocumentId);
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }

        return result;
    }
}
