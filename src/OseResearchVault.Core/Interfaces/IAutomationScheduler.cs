namespace OseResearchVault.Core.Interfaces;

public interface IAutomationScheduler
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
