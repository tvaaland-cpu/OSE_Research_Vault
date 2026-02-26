namespace OseResearchVault.Core.Interfaces;

public interface IMirrorBackupScheduler
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
