using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Repositories;

public sealed class SqliteTradeRepository(IAppSettingsService appSettingsService) : ITradeRepository
{
    public async Task<TradeRecord> CreateTradeAsync(CreateTradeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CompanyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TradeDate);

        var side = request.Side.Trim().ToLowerInvariant();
        if (side is not ("buy" or "sell"))
        {
            throw new ArgumentException("Trade side must be buy or sell.", nameof(request));
        }

        if (request.Quantity <= 0 || request.Price <= 0)
        {
            throw new ArgumentException("Trade quantity and price must be positive.", nameof(request));
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var tradeId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO trade (trade_id, workspace_id, company_id, position_id, trade_date, side, quantity, price, fee, currency, note, source_id, created_at)
              VALUES (@TradeId, @WorkspaceId, @CompanyId, @PositionId, @TradeDate, @Side, @Quantity, @Price, @Fee, @Currency, @Note, @SourceId, @CreatedAt)",
            new
            {
                TradeId = tradeId,
                request.WorkspaceId,
                request.CompanyId,
                request.PositionId,
                request.TradeDate,
                Side = side,
                request.Quantity,
                request.Price,
                request.Fee,
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? "NOK" : request.Currency.Trim().ToUpperInvariant(),
                request.Note,
                request.SourceId,
                CreatedAt = now
            }, cancellationToken: cancellationToken));

        return new TradeRecord
        {
            TradeId = tradeId,
            WorkspaceId = request.WorkspaceId,
            CompanyId = request.CompanyId,
            PositionId = request.PositionId,
            TradeDate = request.TradeDate,
            Side = side,
            Quantity = request.Quantity,
            Price = request.Price,
            Fee = request.Fee,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "NOK" : request.Currency.Trim().ToUpperInvariant(),
            Note = request.Note,
            SourceId = request.SourceId,
            CreatedAt = now
        };
    }

    public async Task<IReadOnlyList<TradeRecord>> ListTradesAsync(string workspaceId, string companyId, string? positionId = null, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<TradeRecord>(new CommandDefinition(
            @"SELECT trade_id AS TradeId,
                     workspace_id AS WorkspaceId,
                     company_id AS CompanyId,
                     position_id AS PositionId,
                     trade_date AS TradeDate,
                     side AS Side,
                     quantity AS Quantity,
                     price AS Price,
                     fee AS Fee,
                     currency AS Currency,
                     note AS Note,
                     source_id AS SourceId,
                     created_at AS CreatedAt
                FROM trade
               WHERE workspace_id = @WorkspaceId
                 AND company_id = @CompanyId
                 AND (@PositionId IS NULL OR position_id = @PositionId)
            ORDER BY trade_date, created_at, trade_id",
            new { WorkspaceId = workspaceId, CompanyId = companyId, PositionId = positionId }, cancellationToken: cancellationToken));

        return results.ToList();
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString());
}
