namespace OseResearchVault.App.ViewModels;

public sealed partial class MainViewModel
{
    public async Task RefreshForWorkspaceSwitchAsync()
    {
        await _importInboxWatcher.ReloadAsync();
        await LoadCompaniesAndTagsAsync();
        await LoadDocumentsAsync();
        await LoadNotesAsync();
        await LoadSearchFiltersAsync();
        await LoadDashboardAsync();
    }
}
