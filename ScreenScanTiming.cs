namespace DwellTargeting;

/// <summary>
/// Shared timing for screen-entry scans — lets UI animate before the first snapshot.
/// </summary>
internal static class ScreenScanTiming
{
    internal const long LayoutSettleMs = 1000;
    internal const long EmptyRetryMs = 250;
    internal const int MaxEmptyRetries = 12;
}
