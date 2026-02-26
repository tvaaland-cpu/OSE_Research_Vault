using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class AutomationEditorDialog : Window
{
    public AutomationEditorDialog(AutomationRecord? existing, IReadOnlyList<AgentTemplateRecord> agents, IReadOnlyList<CompanyRecord> companies)
    {
        InitializeComponent();
        AgentCombo.ItemsSource = agents;

        CompanyScopeCombo.ItemsSource = new[]
        {
            "Global",
            .. companies.Select(c => c.Name),
            "Multiple companies"
        };

        ScheduleTypeCombo.SelectedIndex = 0;
        PayloadTypeCombo.SelectedIndex = 0;
        CompanyScopeCombo.SelectedIndex = 0;
        EnabledCheck.IsChecked = true;
        IntervalText.Text = "60";
        DailyTimeText.Text = "09:00";

        if (existing is null)
        {
            UpdateScheduleVisibility();
            UpdateAgentVisibility();
            return;
        }

        NameText.Text = existing.Name;
        EnabledCheck.IsChecked = existing.Enabled;
        ScheduleTypeCombo.SelectedIndex = string.Equals(existing.ScheduleType, "daily", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        IntervalText.Text = (existing.IntervalMinutes ?? 60).ToString();
        DailyTimeText.Text = existing.DailyTime ?? "09:00";
        PayloadTypeCombo.SelectedIndex = string.Equals(existing.PayloadType, "Agent template", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        AgentCombo.SelectedValue = existing.AgentId;

        if (string.Equals(existing.CompanyScopeMode, "single", StringComparison.OrdinalIgnoreCase))
        {
            var ids = JsonSerializer.Deserialize<List<string>>(existing.CompanyScopeIdsJson) ?? [];
            CompanyScopeCombo.SelectedItem = companies.FirstOrDefault(c => ids.Contains(c.Id))?.Name ?? "Global";
        }
        else if (string.Equals(existing.CompanyScopeMode, "multiple", StringComparison.OrdinalIgnoreCase))
        {
            CompanyScopeCombo.SelectedItem = "Multiple companies";
        }

        QueryText.Text = existing.QueryText;
        UpdateScheduleVisibility();
        UpdateAgentVisibility();
    }

    public AutomationUpsertRequest? Request { get; private set; }

    private void ScheduleTypeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateScheduleVisibility();

    private void PayloadTypeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAgentVisibility();

    private void UpdateScheduleVisibility()
    {
        var isDaily = SelectedScheduleType == "daily";
        IntervalLabel.Visibility = isDaily ? Visibility.Collapsed : Visibility.Visible;
        IntervalText.Visibility = isDaily ? Visibility.Collapsed : Visibility.Visible;
        DailyLabel.Visibility = isDaily ? Visibility.Visible : Visibility.Collapsed;
        DailyTimeText.Visibility = isDaily ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateAgentVisibility()
    {
        var isAgent = SelectedPayloadType == "Agent template";
        AgentLabel.Visibility = isAgent ? Visibility.Visible : Visibility.Collapsed;
        AgentCombo.Visibility = isAgent ? Visibility.Visible : Visibility.Collapsed;
    }

    private string SelectedScheduleType => (ScheduleTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "interval";
    private string SelectedPayloadType => (PayloadTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "AskMyVault";

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameText.Text))
        {
            MessageBox.Show(this, "Name is required.", "Automation Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var interval = int.TryParse(IntervalText.Text, out var parsedInterval) ? parsedInterval : 60;
        var selectedScope = CompanyScopeCombo.SelectedItem?.ToString() ?? "Global";
        var scopeMode = selectedScope == "Global" ? "global" : selectedScope == "Multiple companies" ? "multiple" : "single";

        Request = new AutomationUpsertRequest
        {
            Name = NameText.Text.Trim(),
            Enabled = EnabledCheck.IsChecked == true,
            ScheduleType = SelectedScheduleType,
            IntervalMinutes = interval,
            DailyTime = DailyTimeText.Text.Trim(),
            PayloadType = SelectedPayloadType,
            AgentId = AgentCombo.SelectedValue as string,
            CompanyScopeMode = scopeMode,
            CompanyScopeIds = [],
            QueryText = QueryText.Text.Trim()
        };

        DialogResult = true;
    }
}
