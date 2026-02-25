namespace OseResearchVault.App.ViewModels;

public sealed class CompanyListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public string Isin { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
}
