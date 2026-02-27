using System.Text.RegularExpressions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed partial class MetricService(IMetricRepository metricRepository) : IMetricService
{
    public Task<MetricUpsertResult> UpsertMetricAsync(MetricUpsertRequest request, MetricConflictResolution conflictResolution = MetricConflictResolution.CreateOnly, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UpsertMetricAsync is supported by SqliteMetricService.");

    public Task<string> CreateMetricAsync(MetricCreateRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("CreateMetricAsync(MetricCreateRequest) is supported by SqliteMetricService.");

    public async Task<Metric> CreateMetricAsync(string workspaceId, string companyId, string metricName, string period, double value, string? unit, string? currency, string snippetId, CancellationToken cancellationToken = default)
    {
        ValidateRequired(workspaceId, companyId, metricName, period, value, snippetId);

        return await metricRepository.CreateMetricAsync(
            workspaceId.Trim(),
            companyId.Trim(),
            NormalizeMetricName(metricName),
            period.Trim(),
            value,
            NormalizeOptional(unit),
            NormalizeOptional(currency),
            snippetId.Trim(),
            cancellationToken);
    }

    public Task<IReadOnlyList<Metric>> ListMetricsByCompanyAsync(string workspaceId, string companyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(companyId))
        {
            throw new InvalidOperationException("workspace_id and company_id are required.");
        }

        return metricRepository.ListMetricsByCompanyAsync(workspaceId.Trim(), companyId.Trim(), cancellationToken);
    }

    public Task<IReadOnlyList<Metric>> ListMetricsByCompanyAndNameAsync(string workspaceId, string companyId, string metricName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(companyId) || string.IsNullOrWhiteSpace(metricName))
        {
            throw new InvalidOperationException("workspace_id, company_id, and metric_name are required.");
        }

        return metricRepository.ListMetricsByCompanyAndNameAsync(
            workspaceId.Trim(),
            companyId.Trim(),
            NormalizeMetricName(metricName),
            cancellationToken);
    }

    public async Task<Metric?> UpdateMetricAsync(string workspaceId, string metricId, string metricName, string period, double value, string? unit, string? currency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(metricId))
        {
            throw new InvalidOperationException("workspace_id and metric_id are required.");
        }

        if (string.IsNullOrWhiteSpace(metricName) || string.IsNullOrWhiteSpace(period))
        {
            throw new InvalidOperationException("metric_name and period are required.");
        }

        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException("Metric value must be finite.");
        }

        return await metricRepository.UpdateMetricAsync(
            workspaceId.Trim(),
            metricId.Trim(),
            NormalizeMetricName(metricName),
            period.Trim(),
            value,
            NormalizeOptional(unit),
            NormalizeOptional(currency),
            cancellationToken);
    }

    public Task DeleteMetricAsync(string workspaceId, string metricId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(metricId))
        {
            throw new InvalidOperationException("workspace_id and metric_id are required.");
        }

        return metricRepository.DeleteMetricAsync(workspaceId.Trim(), metricId.Trim(), cancellationToken);
    }

    private static void ValidateRequired(string workspaceId, string companyId, string metricName, string period, double value, string snippetId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("workspace_id is required.");
        }

        if (string.IsNullOrWhiteSpace(companyId))
        {
            throw new InvalidOperationException("company_id is required.");
        }

        if (string.IsNullOrWhiteSpace(metricName))
        {
            throw new InvalidOperationException("metric_name is required.");
        }

        if (string.IsNullOrWhiteSpace(period))
        {
            throw new InvalidOperationException("period is required.");
        }

        if (string.IsNullOrWhiteSpace(snippetId))
        {
            throw new InvalidOperationException("snippet_id is required.");
        }

        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException("Metric value must be finite.");
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeMetricName(string metricName)
    {
        var normalized = MultipleSeparatorRegex().Replace(metricName.Trim().ToLowerInvariant(), "_");
        return normalized.Trim('_');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex MultipleSeparatorRegex();
}
