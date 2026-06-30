namespace DepinTracker.Domain.Enums;

/// <summary>
/// What kind of event reduced a token holding. The FIFO matcher treats all of
/// these uniformly for cost-basis purposes — the distinction is preserved for
/// the tax report so the reader can see whether a "gain" came from a sale or
/// from a swap (which DE tax law also treats as a taxable event).
/// </summary>
public enum DispositionKind
{
    Unknown = 0,
    /// <summary>Sale to fiat (e.g. exchange sale, OTC).</summary>
    Sale = 1,
    /// <summary>Swap to a different crypto asset (DEX or CEX).</summary>
    Swap = 2,
    /// <summary>Spent on a purchase or fee.</summary>
    Spend = 3,
    /// <summary>Transfer to another wallet you own (typically no proceeds).</summary>
    TransferOut = 4,
    /// <summary>Lost to a hack, scam, or smart-contract failure.</summary>
    Loss = 5,
}
