using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DwellTargeting;

/// <summary>
/// Map screen helpers: dwell targets over travelable nodes (hover to pick the next room) plus two
/// pairs of large hover-to-scroll arrows on the left so the map can be moved without dragging.
/// </summary>
internal static class MapOverlay
{
    private const int RescanIntervalFrames = 10;

    private static NMapScreen? _screen;
    private static List<NMapPoint>? _cachedPoints;
    private static int _framesSinceScan;
    private static long _nextSyncLogTick;

    internal static void Sync()
    {
        _screen = OverlayModeService.GetCachedMapScreen();
        if (_screen == null)
        {
            Hide();
            return;
        }

        LeftHoverScrollOverlay.SyncMap();

        _framesSinceScan++;
        if (_cachedPoints == null || _framesSinceScan >= RescanIntervalFrames)
        {
            _framesSinceScan = 0;
            _cachedPoints = FindTravelablePoints(_screen);
        }

        long now = System.Environment.TickCount64;
        if (now >= _nextSyncLogTick)
        {
            _nextSyncLogTick = now + 1500;
            ModLogger.Info($"[Map] sync travelable={_cachedPoints?.Count ?? -1}.");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_screen == null || !NodeQuery.IsLive(_screen) || _cachedPoints == null)
            return;

        int slot = 1;
        foreach (var point in _cachedPoints)
        {
            if (!NodeQuery.IsLive(point) || !NodeQuery.IsVisible(point) || point.State != MapPointState.Travelable)
                continue;

            if (!ControlHitboxService.TryGetDwellRect(point, out var rect))
                continue;

            var captured = point;
            targets.Add(DwellHoverService.Card(rect, () => MapSelectionService.TrySelect(captured), $"MapNode:{slot}"));
            slot++;
        }
    }

    internal static void Hide()
    {
        _screen = null;
        _cachedPoints = null;
        _framesSinceScan = 0;
        LeftHoverScrollOverlay.Hide();
    }

    private static List<NMapPoint> FindTravelablePoints(NMapScreen screen) =>
        NodeQuery.FindAll<NMapPoint>(screen)
            .Where(p => NodeQuery.IsVisible(p) && p.State == MapPointState.Travelable)
            .ToList();
}
