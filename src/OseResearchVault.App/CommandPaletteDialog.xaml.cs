using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OseResearchVault.App;

public partial class CommandPaletteDialog : Window
{
    private readonly List<CommandPaletteItem> _allItems;
    private readonly ObservableCollection<CommandPaletteItem> _filteredItems;

    public CommandPaletteItem? SelectedItem { get; private set; }

    public CommandPaletteDialog(IEnumerable<CommandPaletteItem> items)
    {
        InitializeComponent();
        _allItems = items.ToList();
        _filteredItems = [];
        ResultsListBox.ItemsSource = _filteredItems;
        ApplyFilter(string.Empty);
        Loaded += (_, _) => QueryTextBox.Focus();
    }

    private void QueryTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(QueryTextBox.Text);
    }

    private void ApplyFilter(string query)
    {
        _filteredItems.Clear();

        var ranked = _allItems
            .Select(item => new
            {
                Item = item,
                Score = Score(item.SearchText, query)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.Label, StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .Select(x => x.Item)
            .ToList();

        foreach (var item in ranked)
        {
            _filteredItems.Add(item);
        }

        if (_filteredItems.Count > 0)
        {
            ResultsListBox.SelectedIndex = 0;
        }
    }

    private static int Score(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 100;
        }

        var source = text.ToLowerInvariant();
        var target = query.Trim().ToLowerInvariant();

        if (source.Contains(target, StringComparison.Ordinal))
        {
            return 200 + target.Length;
        }

        var targetChars = target.Where(c => !char.IsWhiteSpace(c)).ToArray();
        if (targetChars.Length == 0)
        {
            return 100;
        }

        var sourceIndex = 0;
        var matched = 0;
        foreach (var c in targetChars)
        {
            var matchIndex = source.IndexOf(c, sourceIndex);
            if (matchIndex < 0)
            {
                return 0;
            }

            matched++;
            sourceIndex = matchIndex + 1;
        }

        return matched == targetChars.Length ? 100 + matched : 0;
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }

    private void ResultsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        CommitSelection();
    }

    private void CommitSelection()
    {
        if (ResultsListBox.SelectedItem is not CommandPaletteItem item)
        {
            return;
        }

        SelectedItem = item;
        DialogResult = true;
        Close();
    }
}

public sealed record CommandPaletteItem(string Label, string Hint, string SearchText, Action Execute);
