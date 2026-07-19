using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace DwellTargeting;

/// <summary>
/// Full deck view: card dwell targets + "View Upgrades" toggle. Card rects are read live each
/// frame from cached holder refs so scrolling stays aligned.
/// </summary>
internal static class DeckViewOverlay
{
    private const float ToggleHitboxPadding = 4f;
    private const long DeckLookupRetryMs = 250;

    private static NDeckViewScreen? _screen;
    private static Control? _viewUpgradesToggle;
    private static ulong _toggleScreenId;
    private static List<NCardHolder>? _cachedHolders;
    private static ScreenEntryScanState _cardScan;
    private static long _lastDeckLookupTick;

    internal static bool IsOpen =>
        _screen != null && NodeQuery.IsLive(_screen) && NodeQuery.IsVisible(_screen);

    internal static int CachedDeckCardCount => _cachedHolders?.Count ?? 0;

    internal static void NotifyClosed()
    {
        if (_screen == null)
            return;

        Hide();
        ViewScreenQuery.Invalidate();
    }

    internal static void Sync()
    {
        TryDiscoverDeckScreen();

        if (_screen != null)
        {
            if (!NodeQuery.IsLive(_screen) || !NodeQuery.IsVisible(_screen))
            {
                NotifyClosed();
                return;
            }

            EnsureToggleCached(_screen);
            SyncDeckCards(_screen);
            return;
        }

        if (_viewUpgradesToggle == null
            && OverlayModeService.TryGetPileSelectScreen(out var pileScreen))
        {
            EnsureToggleCached(pileScreen);
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        TryCollectToggle(_viewUpgradesToggle, targets);

        if (IsOpen && !PileSelectOverlay.IsInConfirmOnlyPhase() && !CardConfirmPhaseQuery.IsActive())
            AppendDeckCardTargets(targets);
    }

    internal static void Hide()
    {
        _screen = null;
        _viewUpgradesToggle = null;
        _toggleScreenId = 0;
        _cachedHolders = null;
        _cardScan.OnHide();
        _lastDeckLookupTick = 0;
    }

    internal static void PrepareForEntry()
    {
        if (PileSelectOverlay.IsInConfirmOnlyPhase())
        {
            RefreshToggleLookup();
            return;
        }

        _viewUpgradesToggle = null;
        _toggleScreenId = 0;
        _cachedHolders = null;
        _cardScan.ScheduleRescan("DeckView");
        ViewScreenQuery.RequestScan();
    }

    internal static void RefreshToggleLookup()
    {
        _viewUpgradesToggle = null;
        _toggleScreenId = 0;
    }

    private static void TryDiscoverDeckScreen()
    {
        if (_screen != null)
            return;

        long now = System.Environment.TickCount64;
        if (!ViewScreenQuery.ShouldAttemptDeckLookup() && now - _lastDeckLookupTick < DeckLookupRetryMs)
            return;

        _lastDeckLookupTick = now;
        _screen = FindOpenDeckView();
        if (_screen == null)
            return;

        ViewScreenQuery.NotifyDeckFound();
        _cardScan.ScheduleRescan("DeckView");
        EnsureToggleCached(_screen);
        ModLogger.Info("[DeckView] deck screen discovered.");
    }

    private static void SyncDeckCards(NDeckViewScreen screen)
    {
        ulong screenId = screen.GetInstanceId();
        bool hasCache = _cachedHolders is { Count: > 0 };
        if (!_cardScan.ShouldScanNow(screenId, hasCache, out _))
            return;

        var holders = FindDeckHolders(screen);
        if (!_cardScan.RegisterScanResult(holders.Count, "DeckView"))
            return;

        _cachedHolders = holders.Count > 0 ? holders : null;
        if (_cachedHolders != null)
            ModLogger.Info($"[DeckView] card snapshot — {_cachedHolders.Count} card(s).");
    }

    private static void AppendDeckCardTargets(List<DwellHoverService.Target> targets)
    {
        if (_cachedHolders == null)
            return;

        int slot = 1;
        foreach (var holder in _cachedHolders)
        {
            if (!NodeQuery.IsLive(holder) || !NodeQuery.IsVisible(holder))
            {
                slot++;
                continue;
            }

            if (!CardAnchorService.TryGetCardRect(holder, out var rect)
                || rect.Size.X < 8f
                || rect.Size.Y < 8f)
            {
                slot++;
                continue;
            }

            var captured = holder;
            int capturedSlot = slot;
            targets.Add(DwellHoverService.Card(
                rect,
                () => ActivateDeckCard(captured, capturedSlot),
                $"DeckCard:{slot}"));
            slot++;
        }
    }

    private static void ActivateDeckCard(NCardHolder holder, int slot)
    {
        if (holder is Control control && InputForwardService.TryActivateControl(control))
        {
            ModLogger.Info($"[DeckView] card {slot} via control click.");
            return;
        }

        PileCardSelectionService.TrySelect(holder, slot);
    }

    private static List<NCardHolder> FindDeckHolders(Node root)
    {
        var list = new List<NCardHolder>();
        foreach (var holder in NodeQuery.FindAll<NCardHolder>(root))
        {
            if (!NodeQuery.IsLive(holder) || !NodeQuery.IsVisible(holder))
                continue;

            if (!CardAnchorService.TryGetCardRect(holder, out var rect))
                continue;

            if (rect.Size.X < 40f || rect.Size.Y < 60f)
                continue;

            if (rect.Position.Y < 100f)
                continue;

            list.Add(holder);
        }

        list.Sort((a, b) =>
        {
            if (a is not Control ca || b is not Control cb)
                return 0;

            int row = ca.GlobalPosition.Y.CompareTo(cb.GlobalPosition.Y);
            return row != 0 ? row : ca.GlobalPosition.X.CompareTo(cb.GlobalPosition.X);
        });

        return list;
    }

    private static void EnsureToggleCached(Node screen)
    {
        ulong screenId = screen.GetInstanceId();
        if (_viewUpgradesToggle != null && _toggleScreenId == screenId
            && NodeQuery.IsLive(_viewUpgradesToggle) && NodeQuery.IsVisible(_viewUpgradesToggle))
        {
            return;
        }

        _toggleScreenId = screenId;
        _viewUpgradesToggle = FindViewUpgradesToggle(screen);
    }

    private static void TryCollectToggle(Control? toggle, List<DwellHoverService.Target> targets)
    {
        if (toggle == null || !NodeQuery.IsLive(toggle) || !NodeQuery.IsVisible(toggle))
            return;

        if (toggle is NClickableControl { IsEnabled: false })
            return;

        var rect = toggle.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return;

        rect = rect.Grow(ToggleHitboxPadding);

        var captured = toggle;
        targets.Add(DwellHoverService.Menu(rect, () => ActivateToggle(captured), "DeckViewUpgrades"));
    }

    private static void ActivateToggle(Control toggle)
    {
        if (!NodeQuery.IsLive(toggle))
            return;

        if (InputForwardService.TryActivateControl(toggle))
        {
            ModLogger.Info($"[DeckView] toggled '{toggle.Name}' ({toggle.GetType().Name}).");
            _cardScan.ScheduleRescan("DeckView");
        }
        else
            ModLogger.Warn($"[DeckView] toggle '{toggle.Name}' activation failed.");
    }

    private static NDeckViewScreen? FindOpenDeckView()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var screen in NodeQuery.FindAll<NDeckViewScreen>(root))
        {
            if (NodeQuery.IsVisible(screen))
                return screen;
        }

        return null;
    }

    private static Control? FindViewUpgradesToggle(Node root)
    {
        foreach (var tickbox in NodeQuery.FindAll<NUpgradePreviewTickbox>(root))
        {
            if (tickbox is Control control && NodeQuery.IsVisible(control))
                return control;
        }

        foreach (var tickbox in NodeQuery.FindAll<NTickbox>(root))
        {
            if (tickbox is not Control control || !NodeQuery.IsVisible(control))
                continue;

            string typeName = tickbox.GetType().Name;
            string nodeName = control.Name.ToString();
            if (typeName.Contains("UpgradePreview", StringComparison.OrdinalIgnoreCase)
                || nodeName.Contains("Upgrade", StringComparison.OrdinalIgnoreCase))
            {
                return control;
            }
        }

        return null;
    }
}
