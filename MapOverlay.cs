using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DwellTargeting;

/// <summary>
/// Map screen helpers: dwell targets over travelable nodes and direct dwell on Proceed/Skip.
/// Hover scroll is handled by <see cref="ViewScrollOverlay"/>.
/// </summary>
internal static class MapOverlay
{
    private const float ProceedHitboxPadding = 24f;

    private static NMapScreen? _screen;
    private static List<NMapPoint>? _cachedPoints;
    private static NProceedButton? _proceedButton;
    private static ScreenEntryScanState _entryScan;

    internal static void Sync()
    {
        _screen = OverlayModeService.GetCachedMapScreen();
        if (_screen == null)
        {
            Hide();
            return;
        }

        ulong screenId = _screen.GetInstanceId();
        if (!_entryScan.ShouldScan(screenId))
            return;

        var points = FindTravelablePoints(_screen);
        _entryScan.MarkScanned(points.Count, "Map");
        _cachedPoints = points;
        _proceedButton = FindProceedButton(_screen);

        if (points.Count > 0)
            ModLogger.Info($"[Map] layout snapshot — {points.Count} travel node(s).");
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

        if (TryGetProceedRect(out var proceedRect))
            targets.Add(DwellHoverService.Menu(proceedRect, ActivateProceed, "MapProceed"));
    }

    internal static void Hide()
    {
        _screen = null;
        _cachedPoints = null;
        _proceedButton = null;
        _entryScan.OnHide();
    }

    internal static void PrepareForEntry() => _entryScan.ScheduleRescan("Map", ScreenScanTiming.MapSettleMs);

    private static NProceedButton? FindProceedButton(NMapScreen screen)
    {
        foreach (var button in NodeQuery.FindAllVisible<NProceedButton>(screen))
        {
            if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;

            return button;
        }

        return null;
    }

    private static bool TryGetProceedRect(out Rect2 rect)
    {
        rect = default;
        if (_proceedButton == null || !NodeQuery.IsLive(_proceedButton) || !NodeQuery.IsVisible(_proceedButton))
            return false;

        if (_proceedButton is NClickableControl { IsEnabled: false })
            return false;

        rect = _proceedButton.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        rect = rect.Grow(ProceedHitboxPadding);
        return true;
    }

    private static void ActivateProceed()
    {
        if (_proceedButton == null || !NodeQuery.IsLive(_proceedButton))
        {
            ModLogger.Warn("[Map] Proceed dwell fired but proceed button missing/dead.");
            return;
        }

        RewardSelectionService.TryProceed(_proceedButton);
    }

    private static List<NMapPoint> FindTravelablePoints(NMapScreen screen) =>
        NodeQuery.FindAll<NMapPoint>(screen)
            .Where(p => NodeQuery.IsVisible(p) && p.State == MapPointState.Travelable)
            .ToList();
}
