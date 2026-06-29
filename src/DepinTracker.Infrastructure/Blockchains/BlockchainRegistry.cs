namespace DepinTracker.Infrastructure.Blockchains;

using DepinTracker.Application.Abstractions;
using DepinTracker.Domain.Entities;
using DepinTracker.Plugins.Abstractions;

/// <summary>
/// Aggregates the blockchains advertised by every loaded explorer plugin into a
/// single lookup. Built once at composition time from the registered explorers, so
/// the available chains always reflect exactly which plugins are loaded.
/// </summary>
public sealed class BlockchainRegistry : IBlockchainRegistry
{
    private readonly IReadOnlyDictionary<string, Blockchain> _byKey;

    public BlockchainRegistry(IEnumerable<IBlockchainExplorer> explorers)
    {
        var map = new Dictionary<string, Blockchain>(StringComparer.OrdinalIgnoreCase);
        foreach (var explorer in explorers)
        {
            foreach (var chain in explorer.SupportedChains)
            {
                map[chain.Key] = chain;
            }
        }

        _byKey = map;
    }

    public IReadOnlyCollection<Blockchain> All => _byKey.Values.ToList();

    public bool TryGet(string key, out Blockchain blockchain)
    {
        if (key is not null && _byKey.TryGetValue(key, out var found))
        {
            blockchain = found;
            return true;
        }

        blockchain = null!;
        return false;
    }
}
