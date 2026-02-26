using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OseResearchVault.App.Logging;
using OseResearchVault.App.Services;
using OseResearchVault.App.ViewModels;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;

namespace OseResearchVault.App;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Starting OSE Research Vault");

        var databaseInitializer = _serviceProvider.GetRequiredService<IDatabaseInitializer>();
        var workspaceService = _serviceProvider.GetRequiredService<IWorkspaceService>();
        if (await workspaceService.GetCurrentAsync() is null)
        {
            var workspaceDialog = _serviceProvider.GetRequiredService<SelectOrCreateWorkspaceDialog>();
            if (workspaceDialog.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        await databaseInitializer.InitializeAsync();

        var automationScheduler = _serviceProvider.GetRequiredService<IAutomationScheduler>();
        await automationScheduler.StartAsync();

        var mirrorBackupScheduler = _serviceProvider.GetRequiredService<IMirrorBackupScheduler>();
        await mirrorBackupScheduler.StartAsync();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            var scheduler = _serviceProvider.GetService<IAutomationScheduler>();
            scheduler?.StopAsync().GetAwaiter().GetResult();

            var mirrorScheduler = _serviceProvider.GetService<IMirrorBackupScheduler>();
            mirrorScheduler?.StopAsync().GetAwaiter().GetResult();
        }

        Services = null;
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddDebug();
            builder.AddProvider(new SimpleFileLoggerProvider());
        });

        services.AddSingleton<IAppSettingsService, JsonAppSettingsService>();
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddSingleton<IHealthRepository, HealthRepository>();
        services.AddSingleton<ISnippetRepository, SqliteSnippetRepository>();
        services.AddSingleton<IEvidenceLinkRepository, SqliteEvidenceLinkRepository>();
        services.AddSingleton<IAutomationRepository, SqliteAutomationRepository>();
        services.AddSingleton<IMetricRepository, SqliteMetricRepository>();
        services.AddSingleton<ITradeRepository, SqliteTradeRepository>();
        services.AddSingleton<IEvidenceService, EvidenceService>();
        services.AddSingleton<IMetricService, MetricService>();
        services.AddSingleton<IFtsSyncService, SqliteFtsSyncService>();
        services.AddSingleton<IDocumentImportService, SqliteDocumentImportService>();
        services.AddSingleton<ICompanyService, SqliteCompanyService>();
        services.AddSingleton<INoteService, SqliteNoteService>();
        services.AddSingleton<IThesisService, SqliteThesisService>();
        services.AddSingleton<ISearchService, SqliteSearchService>();
        services.AddSingleton<IAskMyVaultService, AskMyVaultService>();
        services.AddSingleton<IRetrievalService, SqliteRetrievalService>();
        services.AddSingleton<IAgentService, SqliteAgentService>();
        services.AddSingleton<IDataQualityService, SqliteDataQualityService>();
        services.AddSingleton<IAutomationTemplateService, InMemoryAutomationTemplateService>();
        services.AddSingleton<IImportInboxWatcher, FileSystemImportInboxWatcher>();
        services.AddSingleton<INotificationService, SqliteNotificationService>();
        services.AddSingleton<IAutomationService, SqliteAutomationService>();
        services.AddHttpClient<IConnectorHttpClient, ConnectorHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OSE-Research-Vault/1.0");
        });
        services.AddSingleton<ISnapshotService, SnapshotService>();
        services.AddSingleton<IConnector, DummyUrlSnapshotConnector>();
        services.AddSingleton<IConnector, OseDirectoryCsvConnector>();
        services.AddSingleton<IConnector, AnnouncementsConnector>();
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.AddSingleton<IMetricService, SqliteMetricService>();
        services.AddSingleton<ITradeService, SqliteTradeService>();
        services.AddSingleton<IPositionAnalyticsService, PositionAnalyticsService>();
        services.AddSingleton<IRedactionService, RegexRedactionService>();
        services.AddSingleton<IExportService, SqliteExportService>();
        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddSingleton<IRestoreService, SqliteRestoreService>();
        services.AddSingleton<IShareLogService, SqliteShareLogService>();
        services.AddSingleton<IMemoPublishService, SqliteMemoPublishService>();
        services.AddSingleton<IMetricConflictDialogService, MetricConflictDialogService>();
        services.AddSingleton<ISecretStore, FileSecretStore>();
        services.AddSingleton<ILLMProvider, LocalEchoLlmProvider>();
#if OPENAI_PROVIDER
        services.AddHttpClient<OpenAiLlmProvider>();
        services.AddSingleton<ILLMProvider>(sp => sp.GetRequiredService<OpenAiLlmProvider>());
#endif
#if ANTHROPIC_PROVIDER
        services.AddHttpClient<AnthropicLlmProvider>();
        services.AddSingleton<ILLMProvider>(sp => sp.GetRequiredService<AnthropicLlmProvider>());
#endif
#if GEMINI_PROVIDER
        services.AddHttpClient<GeminiLlmProvider>();
        services.AddSingleton<ILLMProvider>(sp => sp.GetRequiredService<GeminiLlmProvider>());
#endif
        services.AddSingleton<ILLMProviderFactory, LlmProviderFactory>();
        services.AddSingleton<IAutomationExecutor, AutomationExecutor>();
        services.AddSingleton<IAutomationScheduler, AutomationScheduler>();
        services.AddSingleton<IMirrorBackupScheduler, MirrorBackupScheduler>();
        services.AddSingleton<IPromptBuilder, AskVaultPromptBuilder>();
        services.AddSingleton<IInvestmentMemoService, InvestmentMemoService>();
        services.AddSingleton<IReviewService, ReviewService>();
        services.AddSingleton<IUserDialogService, MessageBoxDialogService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<SelectOrCreateWorkspaceDialog>();
        services.AddTransient<WorkspaceManagerDialog>();
        services.AddSingleton<MainWindow>();
    }
}
