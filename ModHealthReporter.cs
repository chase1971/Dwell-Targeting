using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DwellTargeting;

/// <summary>
/// Always-on rolling health snapshot written while the game runs. AI reads
/// %APPDATA%\SlayTheSpire2\logs\dwell-targeting-health.json — no settings toggle required.
/// </summary>
internal static class ModHealthReporter
{
    private const int FlushIntervalMs = 3000;

    private static readonly object Lock = new();
    private static readonly string HealthPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "logs",
        "dwell-targeting-health.json");

    private static long _lastFlushMs;
    private static int _windowFrames;
    private static long _windowFindAllCalls;
    private static long _windowNodesVisited;
    private static int _frameFindAllCalls;
    private static int _frameNodesVisited;
    private static OverlayMode _lastMode = OverlayMode.None;
    private static string _lastModeSnapshot = string.Empty;
    private static int _lastDwellTargets = -1;
    private static bool _frameNoted;
    private static OverlayTargetBreakdown _lastBreakdown = new();

    internal static string SnapshotFilePath => HealthPath;

    internal static void BeginFrame()
    {
        _frameFindAllCalls = 0;
        _frameNodesVisited = 0;
        _frameNoted = false;
    }

    internal static void NoteTreeWalk(int nodesVisited)
    {
        if (nodesVisited <= 0)
            return;

        _frameFindAllCalls++;
        _frameNodesVisited += nodesVisited;
    }

    internal static void NoteFrame(OverlayMode mode, int dwellTargets, string modeSnapshot, IEnumerable<DwellHoverService.Target>? targets = null)
    {
        lock (Lock)
        {
            _lastMode = mode;
            _lastDwellTargets = dwellTargets;
            _lastModeSnapshot = modeSnapshot;
            _lastBreakdown = targets == null ? new OverlayTargetBreakdown() : CountBreakdown(targets);
            _windowFindAllCalls += _frameFindAllCalls;
            _windowNodesVisited += _frameNodesVisited;
            _windowFrames++;
            _frameNoted = true;
        }
    }

    private static OverlayTargetBreakdown CountBreakdown(IEnumerable<DwellHoverService.Target> targets)
    {
        var breakdown = new OverlayTargetBreakdown();
        foreach (var target in targets)
        {
            string name = target.Name;
            if (name.StartsWith("PickCard:", StringComparison.Ordinal)
                || name.StartsWith("PickSkip:", StringComparison.Ordinal))
            {
                breakdown.PileCards++;
            }
            else if (name.StartsWith("DeckCard:", StringComparison.Ordinal))
            {
                breakdown.DeckCards++;
            }
            else if (name.StartsWith("RoomOption:", StringComparison.Ordinal) || name == "RoomProceed")
            {
                breakdown.RoomButtons++;
            }
            else if (name == "BackButton")
            {
                breakdown.HasBack = true;
            }
            else if (name == "DeckViewUpgrades")
            {
                breakdown.HasViewUpgrades = true;
            }
            else if (name == "CardConfirm")
            {
                breakdown.ConfirmOnly = true;
            }
            else if (name == "ManualScan")
            {
                /* utility — not counted */
            }
        }

        return breakdown;
    }

    internal static void EndFrame()
    {
        OverlayMode captureMode;
        int captureTargets;
        int captureFindAll;
        int captureNodes;
        lock (Lock)
        {
            if (!_frameNoted)
            {
                _windowFindAllCalls += _frameFindAllCalls;
                _windowNodesVisited += _frameNodesVisited;
                _windowFrames++;
            }

            captureMode = _lastMode;
            captureTargets = _lastDwellTargets;
            captureFindAll = _frameFindAllCalls;
            captureNodes = _frameNodesVisited;
        }

        FrameFlightRecorder.Capture(
            Environment.TickCount64,
            captureMode,
            captureTargets,
            captureFindAll,
            captureNodes,
            Godot.Engine.GetFramesPerSecond());

        long now = Environment.TickCount64;
        if (now - _lastFlushMs < FlushIntervalMs)
            return;

        Flush(now);
    }

    internal static string BuildSnapshotText()
    {
        lock (Lock)
        {
            double frames = Math.Max(1, _windowFrames);
            double findAllPerFrame = _windowFindAllCalls / frames;
            double nodesPerFrame = _windowNodesVisited / frames;
            var alerts = new List<string>();

            if (findAllPerFrame >= 0.5)
                alerts.Add("RESCAN_STORM");

            if (nodesPerFrame >= 5000)
                alerts.Add("HIGH_TREE_LOAD");

            if (_lastDwellTargets == 0 && IsScreenModeExpectingTargets(_lastMode))
                alerts.Add("NO_TARGETS");

            AppendScreenSpecificAlerts(
                alerts,
                _lastMode,
                _lastBreakdown,
                DeckViewOverlay.IsOpen,
                DeckViewOverlay.CachedDeckCardCount);

            var sb = new StringBuilder();
            sb.AppendLine($"  utc={DateTime.UtcNow:o}");
            sb.AppendLine($"  gameInRun={TryGameInRun()}");
            sb.AppendLine($"  overlayMode={_lastMode}");
            sb.AppendLine($"  modeSnapshot={_lastModeSnapshot}");
            sb.AppendLine($"  dwellTargets={_lastDwellTargets}");
            sb.AppendLine($"  pileCards={_lastBreakdown.PileCards}");
            sb.AppendLine($"  deckCards={_lastBreakdown.DeckCards}");
            sb.AppendLine($"  roomButtons={_lastBreakdown.RoomButtons}");
            sb.AppendLine($"  hasBack={_lastBreakdown.HasBack}");
            sb.AppendLine($"  hasViewUpgrades={_lastBreakdown.HasViewUpgrades}");
            sb.AppendLine($"  confirmOnly={_lastBreakdown.ConfirmOnly}");
            sb.AppendLine($"  findAllCallsPerFrame={findAllPerFrame:F3}");
            sb.AppendLine($"  nodesVisitedPerFrame={nodesPerFrame:F1}");
            sb.AppendLine($"  fps={Godot.Engine.GetFramesPerSecond():F1}");
            sb.AppendLine($"  alerts={(alerts.Count > 0 ? string.Join("; ", alerts) : "none")}");
            sb.AppendLine($"  screenHint={DescribeScreen(_lastMode)}");
            return sb.ToString().TrimEnd();
        }
    }

    internal static void FlushNow() => Flush(Environment.TickCount64);

    private static void Flush(long nowMs)
    {
        HealthSnapshot snapshot;
        lock (Lock)
        {
            if (_windowFrames <= 0)
            {
                _lastFlushMs = nowMs;
                return;
            }

            double frames = _windowFrames;
            snapshot = BuildSnapshot(frames);
            _windowFrames = 0;
            _windowFindAllCalls = 0;
            _windowNodesVisited = 0;
            _lastFlushMs = nowMs;
        }

        WriteSnapshot(snapshot);
    }

    private static HealthSnapshot BuildSnapshot(double frames)
    {
        double findAllPerFrame = _windowFindAllCalls / frames;
        double nodesPerFrame = _windowNodesVisited / frames;
        var alerts = new List<string>();

        if (findAllPerFrame >= 0.5)
            alerts.Add("RESCAN_STORM: full tree walks happening every frame — check scan-state give-up / resolved quiet gate.");

        if (nodesPerFrame >= 5000)
            alerts.Add("HIGH_TREE_LOAD: very high nodes visited per frame.");

        if (_lastDwellTargets == 0 && IsScreenModeExpectingTargets(_lastMode))
            alerts.Add("NO_TARGETS: active screen mode but zero dwell targets collected.");

        if (_lastDwellTargets < 0 && IsScreenModeExpectingTargets(_lastMode))
            alerts.Add("NO_TARGET_DATA: screen mode active but dwell targets were not collected this frame.");

        AppendScreenSpecificAlerts(alerts, _lastMode, _lastBreakdown, DeckViewOverlay.IsOpen, DeckViewOverlay.CachedDeckCardCount);

        return new HealthSnapshot
        {
            UpdatedUtc = DateTime.UtcNow.ToString("o"),
            GameInRun = TryGameInRun(),
            OverlayMode = _lastMode.ToString(),
            ModeSnapshot = _lastModeSnapshot,
            DwellTargets = _lastDwellTargets,
            Targets = _lastBreakdown,
            ScreenHint = DescribeScreen(_lastMode),
            WindowFrames = (int)frames,
            FindAllCallsPerFrame = Math.Round(findAllPerFrame, 3),
            NodesVisitedPerFrame = Math.Round(nodesPerFrame, 1),
            Alerts = alerts,
            LogPath = ModLogger.LogFilePath,
            HealthPath = HealthPath,
            UnitTestCommand = "dotnet test tests/DwellTargeting.Tests.csproj",
        };
    }

    private static bool IsScreenModeExpectingTargets(OverlayMode mode) =>
        mode is OverlayMode.PileSelect
            or OverlayMode.Room
            or OverlayMode.Map
            or OverlayMode.Shop
            or OverlayMode.Event
            or OverlayMode.Rewards;

    private static string DescribeScreen(OverlayMode mode) =>
        mode switch
        {
            OverlayMode.PileSelect => "upgrade/card grid — expect pileCards>0; after pick expect confirmOnly+hasBack",
            OverlayMode.Room => "campfire/chest — expect roomButtons>0 (Rest/Smith)",
            OverlayMode.Map => "map — expect map node targets",
            OverlayMode.Shop => "shop — expect shop item targets",
            OverlayMode.Event => "event — expect event option targets",
            OverlayMode.Rewards => "rewards — expect reward/proceed targets",
            _ => "utility overlays only",
        };

    private static void AppendScreenSpecificAlerts(
        List<string> alerts,
        OverlayMode mode,
        OverlayTargetBreakdown breakdown,
        bool deckViewOpen,
        int deckCardsCached)
    {
        if (deckViewOpen && breakdown.DeckCards == 0 && deckCardsCached == 0)
            alerts.Add("MISSING_DECK_CARDS: deck view open but no DeckCard targets — card scan failed.");

        switch (mode)
        {
            case OverlayMode.PileSelect:
                if (breakdown.ConfirmOnly)
                {
                    if (!breakdown.HasBack)
                        alerts.Add("MISSING_BACK: confirm phase but back button overlay not found.");
                    break;
                }

                if (breakdown.PileCards == 0)
                    alerts.Add("MISSING_PILE_CARDS: PileSelect mode but no PickCard targets — card grid not scanned.");
                if (!breakdown.HasBack)
                    alerts.Add("MISSING_BACK: back button overlay not found.");
                if (!breakdown.HasViewUpgrades)
                    alerts.Add("MISSING_VIEW_UPGRADES: View Upgrades toggle not found (smith grid only).");
                break;
            case OverlayMode.Room:
                if (breakdown.RoomButtons == 0)
                    alerts.Add("MISSING_ROOM_BUTTONS: Room mode but no Rest/Smith/chest targets — scan may be stuck quiet.");
                break;
        }
    }

    private static bool TryGameInRun()
    {
        try
        {
            return MegaCrit.Sts2.Core.Runs.RunManager.Instance.IsInProgress;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteSnapshot(HealthSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HealthPath)!);
            string json = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
            File.WriteAllText(HealthPath, json);
        }
        catch
        {
            /* never break gameplay for telemetry */
        }
    }

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class HealthSnapshot
    {
        [JsonPropertyName("updatedUtc")]
        public string UpdatedUtc { get; set; } = string.Empty;

        [JsonPropertyName("gameInRun")]
        public bool GameInRun { get; set; }

        [JsonPropertyName("overlayMode")]
        public string OverlayMode { get; set; } = string.Empty;

        [JsonPropertyName("modeSnapshot")]
        public string ModeSnapshot { get; set; } = string.Empty;

        [JsonPropertyName("dwellTargets")]
        public int DwellTargets { get; set; }

        [JsonPropertyName("targets")]
        public OverlayTargetBreakdown Targets { get; set; } = new();

        [JsonPropertyName("screenHint")]
        public string ScreenHint { get; set; } = string.Empty;

        [JsonPropertyName("windowFrames")]
        public int WindowFrames { get; set; }

        [JsonPropertyName("findAllCallsPerFrame")]
        public double FindAllCallsPerFrame { get; set; }

        [JsonPropertyName("nodesVisitedPerFrame")]
        public double NodesVisitedPerFrame { get; set; }

        [JsonPropertyName("alerts")]
        public List<string> Alerts { get; set; } = new();

        [JsonPropertyName("logPath")]
        public string LogPath { get; set; } = string.Empty;

        [JsonPropertyName("healthPath")]
        public string HealthPath { get; set; } = string.Empty;

        [JsonPropertyName("unitTestCommand")]
        public string UnitTestCommand { get; set; } = string.Empty;
    }

    internal sealed class OverlayTargetBreakdown
    {
        [JsonPropertyName("pileCards")]
        public int PileCards { get; set; }

        [JsonPropertyName("deckCards")]
        public int DeckCards { get; set; }

        [JsonPropertyName("roomButtons")]
        public int RoomButtons { get; set; }

        [JsonPropertyName("hasBack")]
        public bool HasBack { get; set; }

        [JsonPropertyName("hasViewUpgrades")]
        public bool HasViewUpgrades { get; set; }

        [JsonPropertyName("confirmOnly")]
        public bool ConfirmOnly { get; set; }
    }
}
