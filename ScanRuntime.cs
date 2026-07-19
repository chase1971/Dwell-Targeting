namespace DwellTargeting;

/// <summary>
/// Injectable clock and logging for pure scan-state logic (tests use fakes; mod wires ModLogger at startup).
/// </summary>
internal static class ScanRuntime
{
    internal static Func<long> NowMs = () => System.Environment.TickCount64;
    internal static Action<string> Info = _ => { };
    internal static Action<string> Warn = _ => { };
}
