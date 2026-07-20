using System.Text;

namespace DwellTargeting;

/// <summary>
/// Writes a timestamped feedback beacon dump: current health snapshot plus the rolling
/// flight-recorder window. Appends to dwell-beacon.log and stamps the main log for correlation.
/// </summary>
internal static class FeedbackBeaconService
{
    private static readonly string BeaconPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "logs",
        "dwell-beacon.log");

    private static int _beaconCount;

    internal static void Fire()
    {
        _beaconCount++;
        string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string marker = $"===== BEACON #{_beaconCount} @ {stamp} =====";
        ModLogger.Info(marker);

        var sb = new StringBuilder();
        sb.AppendLine(marker);
        sb.AppendLine("--- snapshot ---");
        sb.AppendLine(ModHealthReporter.BuildSnapshotText());
        sb.AppendLine("--- flight recorder (last ~20s) ---");
        sb.AppendLine(FrameFlightRecorder.DumpText());
        sb.AppendLine();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BeaconPath)!);
            File.AppendAllText(BeaconPath, sb.ToString());
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"[Beacon] failed to write {BeaconPath}: {ex.Message}");
        }

        ModLogger.Info($"[Beacon] feedback saved to {BeaconPath}");
    }
}
