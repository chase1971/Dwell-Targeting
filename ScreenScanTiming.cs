namespace DwellTargeting;

/// <summary>
/// Shared timing for screen-entry scans — lets UI animate before the first snapshot.
/// </summary>
internal static class ScreenScanTiming
{
    internal const long LayoutSettleMs = 1000;
    internal const long CardDraftSettleMs = 400;
    internal const long MapSettleMs = 400;
    internal const long EmptyRetryMs = 250;
    internal const long RescanIntervalMs = 1000;
}
