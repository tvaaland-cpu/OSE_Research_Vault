using System.Collections.ObjectModel;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private NavigationItem _selectedItem;

    public MainViewModel()
    {
        NavigationItems =
        [
            new NavigationItem("Dashboard"),
            new NavigationItem("Companies"),
            new NavigationItem("Watchlist"),
            new NavigationItem("Documents"),
            new NavigationItem("Notes"),
            new NavigationItem("Agents"),
            new NavigationItem("Search"),
            new NavigationItem("Settings")
        ];

        _selectedItem = NavigationItems[0];
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public NavigationItem SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }
}
