using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DwellTargeting;

/// <summary>
/// Hover-to-select for cards on pile/grid/choose/card-reward selection screens. Cards there are
/// <see cref="NCardHolder"/> nodes; we register the card body itself as a dwell target (no number
/// badges — the user wants to hover the card directly, the same way the loot rewards work).
/// </summary>
internal static class PileSelectOverlay
{
    private const int CardRescanIntervalFrames = 5;

    private static Node? _cachedScreen;
    private static List<NCardHolder>? _cachedHolders;
    private static List<Control>? _cachedConfirmButtons;
    private static int _framesSinceCardScan;
    private static long _nextDiagTick;
    private static bool _loggedButtonTypes;

    internal static void Sync()
    {
        if (!OverlayModeService.TryGetPileSelectScreen(out Node screen))
        {
            Hide();
            return;
        }

        bool screenChanged = _cachedScreen != screen;
        if (screenChanged)
            _loggedButtonTypes = false;

        _framesSinceCardScan++;
        if (screenChanged || _cachedHolders == null || _framesSinceCardScan >= CardRescanIntervalFrames)
        {
            _framesSinceCardScan = 0;
            _cachedScreen = screen;
            _cachedHolders = FindSelectableHolders(screen);
            _cachedConfirmButtons = FindConfirmButtons(screen);
        }

        long now = System.Environment.TickCount64;
        if (now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info($"[Pile] sync screen={screen.GetType().Name} holders={_cachedHolders.Count} confirm={_cachedConfirmButtons?.Count ?? 0}.");

            if ((_cachedConfirmButtons?.Count ?? 0) == 0 && !_loggedButtonTypes)
            {
                _loggedButtonTypes = true;
                LogButtonLikeTypes(screen);
            }
        }

    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_cachedHolders != null)
        {
            int slot = 1;
            foreach (var holder in _cachedHolders)
            {
                if (!NodeQuery.IsLive(holder))
                {
                    slot++;
                    continue;
                }

                if (CardAnchorService.TryGetCardRect(holder, out var cardRect)
                    && cardRect.Size.X >= 8f && cardRect.Size.Y >= 8f)
                {
                    var captured = holder;
                    int capturedSlot = slot;
                    // Menu timing (slower + cooldown) so a card pick is deliberate, not instant.
                    targets.Add(DwellHoverService.Menu(
                        cardRect,
                        () => PileCardSelectionService.TrySelect(captured, capturedSlot),
                        $"PileCard:{slot}"));
                }

                slot++;
            }
        }

        if (_cachedConfirmButtons != null)
        {
            foreach (var button in _cachedConfirmButtons)
            {
                if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
                    continue;
                if (button is NClickableControl { IsEnabled: false })
                    continue;

                if (ControlHitboxService.TryGetDwellRect(button, out var rect))
                {
                    var captured = button;
                    targets.Add(DwellHoverService.Menu(
                        rect,
                        () => InputForwardService.TryActivateControl(captured),
                        $"PileConfirm:{button.Name}"));
                }
            }
        }
    }

    internal static void Hide()
    {
        _cachedScreen = null;
        _cachedHolders = null;
        _cachedConfirmButtons = null;
        _framesSinceCardScan = 0;
        _loggedButtonTypes = false;
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        return false;
    }

    // Keep scene-tree order (stable across rescans).
    private static List<NCardHolder> FindSelectableHolders(Node screen) =>
        NodeQuery.FindAll<NCardHolder>(screen)
            .Where(IsSelectableHolder)
            .ToList();

    // The post-selection Confirm / Skip / Proceed buttons on these screens are NButton/NClickableControl,
    // so a ForceClick activates them (the "E" key does not work for the user's card-reward proceed).
    private static List<Control> FindConfirmButtons(Node screen)
    {
        var list = new List<Control>();

        foreach (var b in NodeQuery.FindAll<NConfirmButton>(screen))
            if (b is Control c && NodeQuery.IsVisible(c))
                list.Add(c);

        foreach (var b in NodeQuery.FindAll<NChoiceSelectionSkipButton>(screen))
            if (b is Control c && NodeQuery.IsVisible(c))
                list.Add(c);

        foreach (var b in NodeQuery.FindAll<NProceedButton>(screen))
            if (b is Control c && NodeQuery.IsVisible(c))
                list.Add(c);

        // The card-reward "Choose a Card" Skip is an NCardRewardAlternativeButton (no shared base we
        // can reference here), so match it by type name and treat it as a skip/confirm target.
        AddControlsByTypeName(screen, list, "NCardRewardAlternativeButton");

        return list;
    }

    private static void AddControlsByTypeName(Node node, List<Control> list, string typeName)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (node is Control control
            && node.GetType().Name == typeName
            && NodeQuery.IsVisible(control)
            && control is not NClickableControl { IsEnabled: false }
            && !list.Contains(control))
        {
            list.Add(control);
        }

        try
        {
            foreach (var child in node.GetChildren())
                AddControlsByTypeName(child, list, typeName);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    private static void LogButtonLikeTypes(Node screen)
    {
        var seen = new HashSet<string>();
        CollectButtonLikeTypes(screen, seen);
        ModLogger.Info($"[Pile] no confirm/skip found. Button-like node types under {screen.GetType().Name}: {string.Join(", ", seen)}");
    }

    private static void CollectButtonLikeTypes(Node node, HashSet<string> seen)
    {
        if (!NodeQuery.IsLive(node))
            return;

        string name = node.GetType().Name;
        if (name.Contains("Button", StringComparison.Ordinal)
            || name.Contains("Skip", StringComparison.Ordinal)
            || name.Contains("Proceed", StringComparison.Ordinal)
            || name.Contains("Confirm", StringComparison.Ordinal))
        {
            seen.Add(name);
        }

        try
        {
            foreach (var child in node.GetChildren())
                CollectButtonLikeTypes(child, seen);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    private static bool IsSelectableHolder(NCardHolder holder)
    {
        if (!NodeQuery.IsLive(holder) || !NodeQuery.IsVisible(holder))
            return false;
        if (holder.CardModel == null)
            return false;
        if (!CardAnchorService.TryGetCardRect(holder, out var rect))
            return false;
        return rect.Size.X >= 60f && rect.Size.Y >= 80f;
    }
}
