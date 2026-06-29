namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;

/// <summary>
/// Dapper-backed <see cref="IUserSettingsStore"/> over the config store's
/// <c>settings</c> table. Settings are simple key→value strings; callers serialize
/// non-string values themselves so the storage stays format-agnostic.
/// </summary>
public sealed class UserSettingsStore : IUserSettingsStore
{
    private readonly ISqliteConnectionFactory _factory;

    public UserSettingsStore(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Value FROM settings WHERE Key = @key;",
            new { key }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        if (string.IsNullOrEmpty(value))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM settings WHERE Key = @key;",
                new { key }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO settings (Key, Value) VALUES (@key, @value)
            ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value;
            """,
            new { key, value }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
