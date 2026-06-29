namespace DepinTracker.Application.Abstractions;

using DepinTracker.Domain.Entities;

/// <summary>
/// The set of blockchains known to the running app, aggregated from every loaded
/// explorer plugin. Lets the UI offer chains and lets services resolve a chain key.
/// </summary>
public interface IBlockchainRegistry
{
    IReadOnlyCollection<Blockchain> All { get; }
    bool TryGet(string key, out Blockchain blockchain);
}
