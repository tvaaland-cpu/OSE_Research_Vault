using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OseResearchVault.App.Logging;
using OseResearchVault.App.ViewModels;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;

namespace OseResearchVault.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Starting OSE Research Vault");

        var databaseInitializer = _serviceProvider.GetRequiredService<IDatabaseInitializer>();
        await databaseInitializer.InitializeAsync();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddSingleton<IHealthRepository, HealthRepository>();
        services.AddSingleton<ISnippetRepository, SqliteSnippetRepository>();
        services.AddSingleton<IEvidenceLinkRepository, SqliteEvidenceLinkRepository>();
        services.AddSingleton<IEvidenceService, EvidenceService>();
        services.AddSingleton<IFtsSyncService, SqliteFtsSyncService>();
        services.AddSingleton<IDocumentImportService, SqliteDocumentImportService>();
        services.AddSingleton<ICompanyService, SqliteCompanyService>();
        services.AddSingleton<INoteService, SqliteNoteService>();
        services.AddSingleton<ISearchService, SqliteSearchService>();
        services.AddSingleton<IAskMyVaultService, AskMyVaultService>();
        services.AddSingleton<IAgentService, SqliteAgentService>();
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

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
