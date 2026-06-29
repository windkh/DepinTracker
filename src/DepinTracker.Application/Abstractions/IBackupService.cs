namespace DepinTracker.Application.Abstractions;

/// <summary>
/// Full backup / restore / integrity verification of the portable data stores.
/// Implemented in Infrastructure (zips the db files with a hash manifest).
/// </summary>
public interface IBackupService
{
    /// <summary>Creates a backup archive under the backups folder and returns its path.</summary>
    Task<string> CreateBackupAsync(CancellationToken cancellationToken);

    /// <summary>Restores the data stores from a backup archive.</summary>
    Task RestoreAsync(string archivePath, CancellationToken cancellationToken);

    /// <summary>Verifies a backup archive against its integrity manifest.</summary>
    Task<bool> VerifyAsync(string archivePath, CancellationToken cancellationToken);
}
