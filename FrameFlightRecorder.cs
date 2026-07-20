using System.Text;

namespace DwellTargeting;

/// <summary>
/// Rolling ring buffer of per-frame health samples (~20s at 60 FPS). When the feedback beacon
/// fires, the last window is dumped so the player never has to react instantly.
/// </summary>
internal static class FrameFlightRecorder
{
    private const int Capacity = 1200;

    private static readonly FrameRecord[] Buffer = new FrameRecord[Capacity];
    private static int _writeIndex;
    private static int _count;

    internal static void Capture(
        long tickMs,
        OverlayMode mode,
        int dwellTargets,
        int findAllCalls,
        int nodesVisited,
        double fps)
    {
        Buffer[_writeIndex] = new FrameRecord
        {
            TickMs = tickMs,
            Mode = mode,
            DwellTargets = dwellTargets,
            FindAllCalls = findAllCalls,
            NodesVisited = nodesVisited,
            Fps = fps,
        };

        _writeIndex = (_writeIndex + 1) % Capacity;
        if (_count < Capacity)
            _count++;
    }

    internal static string DumpText()
    {
        if (_count == 0)
            return "  (flight recorder empty)";

        var sb = new StringBuilder();
        sb.AppendLine($"  frames={_count} windowSec~{_count / 60.0:F1}");

        int start = _count < Capacity ? 0 : _writeIndex;
        for (int i = 0; i < _count; i++)
        {
            int idx = (start + i) % Capacity;
            var record = Buffer[idx];
            sb.Append("  ");
            sb.Append(record.TickMs);
            sb.Append(" mode=");
            sb.Append(record.Mode);
            sb.Append(" targets=");
            sb.Append(record.DwellTargets);
            sb.Append(" walks=");
            sb.Append(record.FindAllCalls);
            sb.Append(" nodes=");
            sb.Append(record.NodesVisited);
            sb.Append(" fps=");
            sb.AppendLine(record.Fps.ToString("F1"));
        }

        return sb.ToString().TrimEnd();
    }

    private struct FrameRecord
    {
        internal long TickMs;
        internal OverlayMode Mode;
        internal int DwellTargets;
        internal int FindAllCalls;
        internal int NodesVisited;
        internal double Fps;
    }
}
