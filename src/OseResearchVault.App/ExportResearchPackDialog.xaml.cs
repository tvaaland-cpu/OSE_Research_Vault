using System.Windows;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class ExportResearchPackDialog : Window
{
    private readonly IReadOnlyList<ExportProfileRecord> _profiles;

    public ExportResearchPackDialog(IReadOnlyList<ExportProfileRecord> profiles)
    {
        InitializeComponent();
        _profiles = profiles;
        ProfileComboBox.ItemsSource = _profiles;
        ProfileComboBox.SelectedIndex = _profiles.Count > 0 ? 0 : -1;
        UpdateProfileSelectorState();
    }

    public bool ApplyRedaction => ApplyRedactionCheckBox.IsChecked ?? false;

    public string? SelectedProfileId => ProfileComboBox.SelectedValue?.ToString();

    public bool ExcludePrivateTaggedItems => ExcludePrivateCheckBox.IsChecked ?? true;

    private void ApplyRedactionCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateProfileSelectorState();
    }

    private void Export_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void UpdateProfileSelectorState()
    {
        ProfileComboBox.IsEnabled = ApplyRedaction;
    }
}
