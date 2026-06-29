namespace DepinTracker.Application.Dtos;

/// <summary>Summary returned to the UI after an import run.</summary>
public sealed record ImportResult(
    Guid ImportSessionId,
    int Imported,
    int Skipped,
    bool Success,
    string? Message);
