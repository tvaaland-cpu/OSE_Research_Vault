using System.Windows;

namespace OseResearchVault.App;

public partial class FetchAnnouncementsDialog : Window
{
    public int Days { get; set; } = 30;
    public string ManualUrls { get; set; } = string.Empty;

    public FetchAnnouncementsDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Fetch_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
