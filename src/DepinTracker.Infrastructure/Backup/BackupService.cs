namespace DepinTracker.Infrastructure.Backup;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DepinTracker.Application.Abstractions;
using DepinTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

/// <summary>
/// Full backup / restore / verify of the three SQLite stores. A backup is a zip of
/// the database files plus a <c>manifest.json</c> listing SHA-256 hashes; restore
/// verifies those hashes before overwriting, and verify checks an archive without
/// touching live data. WAL/SHM sidecar files are included when present.
/// </summary>
public sealed class BackupService : IBackupService
{
    private const string ManifestEntry = "manifest.json";

    private readonly ISqliteConnectionFactory _factory;
    private readonly IAppPaths _paths;
    private readonly IClock _clock;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        ISqliteConnectionFactory factory, IAppPaths paths, IClock clock, ILogger<BackupService> logger)
    {
        _factory = factory;
        _paths = paths;
        _clock = clock;
        _logger = logger;
    }

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.Backups);
        Checkpoint();

        var stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss");
        var archivePath = Path.Combine(_paths.Backups, $"backup-{stamp}.zip");
        var manifest = new Dictionary<string, string>(StringComparer.Ordinal);

        await using (var zipStream = File.Create(archivePath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            foreach (var file in EnumerateStoreFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entryName = Path.GetFileName(file);
                archive.CreateEntryFromFile(file, entryName);
                manifest[entryName] = await ComputeHashAsync(file, cancellationToken).ConfigureAwait(false);
            }

            var manifestEntry = archive.CreateEntry(ManifestEntry);
            await using var entryStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(
                entryStream, manifest, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("Created backup {Path}", archivePath);
        return archivePath;
    }

    public async Task<bool> VerifyAsync(string archivePath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var manifest = await ReadManifestAsync(archive, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return false;
        }

        foreach (var (name, expectedHash) in manifest)
        {
            var entry = archive.GetEntry(name);
            if (entry is null)
            {
                return false;
            }

            await using var stream = entry.Open();
            if (!string.Equals(await ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public async Task RestoreAsync(string archivePath, CancellationToken cancellationToken)
    {
        if (!await VerifyAsync(archivePath, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Backup '{archivePath}' failed integrity verification; restore aborted.");
        }

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (string.Equals(entry.Name, ManifestEntry, StringComparison.Ordinal))
            {
                continue;
            }

            var target = ResolveTargetPath(entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }

        _logger.LogInformation("Restored data stores from {Path}", archivePath);
    }

    private void Checkpoint()
    {
        foreach (var store in Enum.GetValues<StoreKind>())
        {
            try
            {
                using var connection = _factory.CreateOpenConnection(store);
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WAL checkpoint failed for {Store}", store);
            }
        }
    }

    private IEnumerable<string> EnumerateStoreFiles()
    {
        foreach (var store in Enum.GetValues<StoreKind>())
        {
            var dbPath = _factory.GetDatabasePath(store);
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var path = dbPath + suffix;
                if (File.Exists(path))
                {
                    yield return path;
                }
            }
        }
    }

    private string ResolveTargetPath(string entryName)
    {
        // Map a backed-up file name back to its store location by base db file name.
        foreach (var store in Enum.GetValues<StoreKind>())
        {
            var dbPath = _factory.GetDatabasePath(store);
            var baseName = Path.GetFileName(dbPath);
            if (entryName == baseName || entryName.StartsWith(baseName + "-", StringComparison.Ordinal))
            {
                var dir = Path.GetDirectoryName(dbPath)!;
                return Path.Combine(dir, entryName);
            }
        }

        // Fallback: restore into the data folder.
        return Path.Combine(_paths.Data, entryName);
    }

    private static async Task<Dictionary<string, string>?> ReadManifestAsync(
        ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(ManifestEntry);
        if (entry is null)
        {
            return null;
        }

        await using var stream = entry.Open();
        return await JsonSerializer
            .DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
