using System.Windows;
using OseResearchVault.App.ViewModels;

namespace OseResearchVault.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
