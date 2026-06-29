namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>Dapper-backed <see cref="IWalletRepository"/> over the config store.</summary>
public sealed class WalletRepository : IWalletRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly TagWriter _tags;

    public WalletRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
        _tags = new TagWriter(factory);
    }

    public async Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        var wallet = await connection.QuerySingleOrDefaultAsync<Wallet>(new CommandDefinition(
            "SELECT * FROM wallets WHERE Id = @id;", new { id }, cancellationToken: cancellationToken));
        if (wallet is not null)
        {
            wallet.Tags = await _tags.LoadAsync("wallet_tags", "WalletId", id, cancellationToken);
        }

        return wallet;
    }

    public async Task<IReadOnlyList<Wallet>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        var wallets = (await connection.QueryAsync<Wallet>(new CommandDefinition(
            "SELECT * FROM wallets ORDER BY CreatedUtc;", cancellationToken: cancellationToken))).ToList();
        foreach (var wallet in wallets)
        {
            wallet.Tags = await _tags.LoadAsync("wallet_tags", "WalletId", wallet.Id, cancellationToken);
        }

        return wallets;
    }

    public async Task<IReadOnlyList<Wallet>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        var wallets = (await connection.QueryAsync<Wallet>(new CommandDefinition(
            "SELECT * FROM wallets WHERE ProjectId = @projectId ORDER BY CreatedUtc;",
            new { projectId }, cancellationToken: cancellationToken))).ToList();
        foreach (var wallet in wallets)
        {
            wallet.Tags = await _tags.LoadAsync("wallet_tags", "WalletId", wallet.Id, cancellationToken);
        }

        return wallets;
    }

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO wallets (Id, ProjectId, BlockchainKey, Address, Label, Notes, IsActive, CreatedUtc)
            VALUES (@Id, @ProjectId, @BlockchainKey, @Address, @Label, @Notes, @IsActive, @CreatedUtc);
            """,
            wallet, cancellationToken: cancellationToken));
        await _tags.SyncAsync("wallet_tags", "WalletId", wallet.Id, wallet.Tags, cancellationToken);
    }

    public async Task UpdateAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE wallets
               SET ProjectId = @ProjectId, BlockchainKey = @BlockchainKey, Address = @Address,
                   Label = @Label, Notes = @Notes, IsActive = @IsActive
             WHERE Id = @Id;
            """,
            wallet, cancellationToken: cancellationToken));
        await _tags.SyncAsync("wallet_tags", "WalletId", wallet.Id, wallet.Tags, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM wallets WHERE Id = @id;", new { id }, cancellationToken: cancellationToken));
    }
}
