using System.Collections.ObjectModel;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App.ViewModels;

public sealed partial class MainViewModel
{
    private ModelProfileListItemViewModel? _selectedModelProfile;
    private ModelProfileListItemViewModel? _selectedAgentModelProfile;
    private string _modelProfileName = string.Empty;
    private string _modelProfileProvider = "openai";
    private string _modelProfileModel = string.Empty;
    private string _modelProfileParametersJson = "{}";
    private bool _modelProfileIsDefault;

    public ObservableCollection<ModelProfileListItemViewModel> ModelProfiles { get; } = [];
    public ModelProfileListItemViewModel? SelectedModelProfile { get => _selectedModelProfile; set => SetProperty(ref _selectedModelProfile, value); }
    public ModelProfileListItemViewModel? SelectedAgentModelProfile { get => _selectedAgentModelProfile; set => SetProperty(ref _selectedAgentModelProfile, value); }
    public string ModelProfileName { get => _modelProfileName; set => SetProperty(ref _modelProfileName, value); }
    public string ModelProfileProvider { get => _modelProfileProvider; set => SetProperty(ref _modelProfileProvider, value); }
    public string ModelProfileModel { get => _modelProfileModel; set => SetProperty(ref _modelProfileModel, value); }
    public string ModelProfileParametersJson { get => _modelProfileParametersJson; set => SetProperty(ref _modelProfileParametersJson, value); }
    public bool ModelProfileIsDefault { get => _modelProfileIsDefault; set => SetProperty(ref _modelProfileIsDefault, value); }

    public RelayCommand NewModelProfileCommand => new(ClearModelProfileForm);
    public RelayCommand SaveModelProfileCommand => new(() => _ = SaveModelProfileAsync());
    public RelayCommand DeleteModelProfileCommand => new(() => _ = DeleteModelProfileAsync(), () => SelectedModelProfile is not null);
    public RelayCommand SetDefaultModelProfileCommand => new(() => _ = SetDefaultModelProfileAsync(), () => SelectedModelProfile is not null);

    private async Task LoadModelProfilesAsync()
    {
        var profiles = await _agentService.GetModelProfilesAsync();
        ModelProfiles.Clear();
        foreach (var profile in profiles)
        {
            ModelProfiles.Add(new ModelProfileListItemViewModel
            {
                ModelProfileId = profile.ModelProfileId,
                Name = profile.Name,
                Provider = profile.Provider,
                Model = profile.Model,
                ParametersJson = profile.ParametersJson,
                IsDefault = profile.IsDefault
            });
        }

        SelectedAgentModelProfile = ModelProfiles.FirstOrDefault(p => p.IsDefault) ?? ModelProfiles.FirstOrDefault();
    }

    private async Task SaveModelProfileAsync()
    {
        var request = new ModelProfileUpsertRequest
        {
            Name = ModelProfileName,
            Provider = ModelProfileProvider,
            Model = ModelProfileModel,
            ParametersJson = ModelProfileParametersJson,
            IsDefault = ModelProfileIsDefault
        };

        if (SelectedModelProfile is null)
        {
            await _agentService.CreateModelProfileAsync(request);
        }
        else
        {
            await _agentService.UpdateModelProfileAsync(SelectedModelProfile.ModelProfileId, request);
        }

        await LoadModelProfilesAsync();
    }

    private async Task DeleteModelProfileAsync()
    {
        if (SelectedModelProfile is null)
        {
            return;
        }

        await _agentService.DeleteModelProfileAsync(SelectedModelProfile.ModelProfileId);
        await LoadModelProfilesAsync();
    }

    private async Task SetDefaultModelProfileAsync()
    {
        if (SelectedModelProfile is null)
        {
            return;
        }

        await _agentService.SetDefaultModelProfileAsync(SelectedModelProfile.ModelProfileId);
        await LoadModelProfilesAsync();
    }

    private void ClearModelProfileForm()
    {
        SelectedModelProfile = null;
        ModelProfileName = string.Empty;
        ModelProfileProvider = "openai";
        ModelProfileModel = string.Empty;
        ModelProfileParametersJson = "{}";
        ModelProfileIsDefault = false;
    }
}
