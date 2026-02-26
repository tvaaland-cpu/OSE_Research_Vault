using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace OseResearchVault.App.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly JsonSerializerOptions TemplateJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ObservableCollection<NoteGalleryTemplate> NoteTemplates { get; } = new(LoadTemplates<NoteGalleryTemplate>("notes.json"));
    public ObservableCollection<AgentGalleryTemplate> AgentGalleryTemplates { get; } = new(LoadTemplates<AgentGalleryTemplate>("agents.json"));
    public ObservableCollection<AutomationGalleryTemplate> AutomationGalleryTemplates { get; } = new(LoadTemplates<AutomationGalleryTemplate>("automations.json"));

    public RelayCommand UseNoteTemplateCommand => new(param => UseNoteTemplate(param as NoteGalleryTemplate));
    public RelayCommand UseAgentGalleryTemplateCommand => new(param => _ = UseAgentTemplateAsync(param as AgentGalleryTemplate));
    public RelayCommand UseAutomationGalleryTemplateCommand => new(param => UseAutomationTemplate(param as AutomationGalleryTemplate));

    public bool IsTemplatesSelected => IsSelected("Templates");

    private void UseNoteTemplate(NoteGalleryTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        NoteTitle = template.Title;
        NoteContent = template.Content;
        SelectedNoteType = string.IsNullOrWhiteSpace(template.NoteType) ? "manual" : template.NoteType;
        NoteTags = template.Tags.Count == 0 ? string.Empty : string.Join(',', template.Tags);

        if (SaveNoteCommand.CanExecute(null))
        {
            SaveNoteCommand.Execute(null);
        }
    }

    private async Task UseAgentTemplateAsync(AgentGalleryTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        AgentName = template.Name;
        AgentGoal = template.Goal;
        AgentInstructions = template.Instructions;
        AgentAllowedTools = JsonSerializer.Serialize(template.AllowedTools);
        AgentEvidencePolicy = template.EvidencePolicy;
        AgentOutputSchema = template.OutputSchema;

        if (SaveAgentTemplateCommand.CanExecute(null))
        {
            SaveAgentTemplateCommand.Execute(null);
            await Task.Delay(1);
        }
    }

    private void UseAutomationTemplate(AutomationGalleryTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        CreateAutomationFromTemplate(template.AutomationTemplateId);
    }

    private static IReadOnlyList<TTemplate> LoadTemplates<TTemplate>(string fileName)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "assets", "templates", fileName);
        if (!File.Exists(filePath))
        {
            return [];
        }

        using var stream = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<List<TTemplate>>(stream, TemplateJsonOptions) ?? [];
    }
}

public sealed class NoteGalleryTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string NoteType { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Content { get; set; } = string.Empty;
}

public sealed class AgentGalleryTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public List<string> AllowedTools { get; set; } = [];
    public string OutputSchema { get; set; } = string.Empty;
    public string EvidencePolicy { get; set; } = string.Empty;
}

public sealed class AutomationGalleryTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AutomationTemplateId { get; set; } = string.Empty;
}
