using System.Windows;
using OseResearchVault.App.ViewModels;

namespace OseResearchVault.App;

public partial class NewNoteDialog : Window
{
    public string NoteTitle => TitleTextBox.Text.Trim();
    public string NoteContent => ContentTextBox.Text;
    public string NoteType => NoteTypeComboBox.SelectedItem as string ?? "manual";
    public CompanyOptionViewModel? SelectedCompany => CompanyComboBox.SelectedItem as CompanyOptionViewModel;

    public NewNoteDialog(IEnumerable<CompanyOptionViewModel> companyOptions, IEnumerable<string> noteTypes, CompanyOptionViewModel? selectedCompany)
    {
        InitializeComponent();
        NoteTypeComboBox.ItemsSource = noteTypes.ToList();
        NoteTypeComboBox.SelectedIndex = 0;

        CompanyComboBox.ItemsSource = companyOptions.ToList();
        CompanyComboBox.SelectedItem = selectedCompany;

        Loaded += (_, _) => TitleTextBox.Focus();
    }

    private void Create_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NoteTitle))
        {
            MessageBox.Show(this, "Note title is required.", "New Note", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
