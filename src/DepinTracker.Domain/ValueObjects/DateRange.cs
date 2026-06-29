namespace DepinTracker.Domain.ValueObjects;

/// <summary>
/// An inclusive range of calendar dates (UTC, date-only). Used by analytics,
/// tax reports and the rebuild engine to bound work to a period such as a tax year.
/// </summary>
public readonly record struct DateRange
{
    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException($"DateRange end ({end}) must not precede start ({start}).");
        }

        Start = start;
        End = end;
    }

    public DateOnly Start { get; }
    public DateOnly End { get; }

    /// <summary>The full calendar year, e.g. <c>DateRange.Year(2025)</c>.</summary>
    public static DateRange Year(int year) =>
        new(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public override string ToString() => $"{Start:yyyy-MM-dd}..{End:yyyy-MM-dd}";
}
