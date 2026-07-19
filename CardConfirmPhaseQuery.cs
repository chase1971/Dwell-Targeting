using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DwellTargeting;

/// <summary>
/// Card upgrade / removal / pile-select confirmation: native checkmark + back only — no grid picks.
/// Confirm lookup is cached per screen; no repeated root walks while the layout is stable.
/// </summary>
internal static class CardConfirmPhaseQuery
{
    private const float ConfirmPadding = 12f;

    private static bool _lookupValid;
    private static bool _confirmPresent;
    private static Control? _cachedConfirmButton;
    private static ulong _cachedScreenId;

    internal static void InvalidateCache()
    {
        _lookupValid = false;
        _confirmPresent = false;
        _cachedConfirmButton = null;
        _cachedScreenId = 0;
    }

    internal static bool IsActive()
    {
        var mode = OverlayModeService.GetMode();
        if (mode is not (OverlayMode.HandSelect or OverlayMode.PileSelect))
        {
            InvalidateCache();
            return false;
        }

        EnsureLookup(mode);
        if (!_confirmPresent)
            return false;

        if (mode == OverlayMode.PileSelect)
        {
            if (!_confirmPresent)
                return false;

            if (PileSelectOverlay.IsAwaitingConfirm() || PilePreviewQuery.IsUpgradeConfirmFlowActive())
                return true;

            return OverlayModeService.TryGetPileSelectScreen(out var pileScreen)
                && (HasPileSelectionPreview(pileScreen)
                    || PilePreviewQuery.HasLargeCenterPreviewCards(pileScreen));
        }

        return HasSelectedCardPreview();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (!IsActive())
            return;

        EnsureLookup(OverlayModeService.GetMode());
        if (_cachedConfirmButton == null || !NodeQuery.IsLive(_cachedConfirmButton))
            return;

        if (!TryGetDwellRect(_cachedConfirmButton, out var rect))
            return;

        var captured = _cachedConfirmButton;
        targets.Add(DwellHoverService.Menu(
            rect,
            () => ActivateConfirm(captured),
            "CardConfirm"));
    }

    private static void EnsureLookup(OverlayMode mode)
    {
        ulong screenId = ResolveScreenId(mode);
        if (_lookupValid && _cachedScreenId == screenId)
            return;

        _cachedScreenId = screenId;
        _lookupValid = true;
        _confirmPresent = TryFindConfirmButton(out _cachedConfirmButton);
    }

    private static ulong ResolveScreenId(OverlayMode mode)
    {
        if (mode == OverlayMode.PileSelect
            && OverlayModeService.TryGetPileSelectScreen(out var pileScreen))
        {
            return pileScreen.GetInstanceId();
        }

        var hand = NPlayerHand.Instance;
        if (hand != null && NodeQuery.IsLive(hand))
            return hand.GetInstanceId();

        return 0;
    }

    private static bool HasSelectedCardPreview()
    {
        var hand = NPlayerHand.Instance;
        if (hand == null || !NodeQuery.IsLive(hand))
            return false;

        foreach (var container in NodeQuery.FindAll<NSelectedHandCardContainer>(hand))
        {
            if (container is Control control && NodeQuery.IsVisible(control))
                return true;
        }

        return false;
    }

    private static bool HasPileSelectionPreview(Node pileScreen)
    {
        foreach (var container in NodeQuery.FindAll<NSelectedHandCardContainer>(pileScreen))
        {
            if (container is Control control && NodeQuery.IsVisible(control))
                return true;
        }

        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return false;

        foreach (var container in NodeQuery.FindAll<NSelectedHandCardContainer>(root))
        {
            if (container is Control control && NodeQuery.IsVisible(control))
                return true;
        }

        return false;
    }

    private static bool TryFindConfirmButton(out Control? button)
    {
        button = null;
        Node? searchRoot = null;

        if (OverlayModeService.TryGetPileSelectScreen(out var pileScreen))
            searchRoot = pileScreen;

        if (searchRoot == null)
        {
            var hand = NPlayerHand.Instance;
            if (hand != null && NodeQuery.IsLive(hand))
                searchRoot = hand;
        }

        if (searchRoot == null)
        {
            searchRoot = (Engine.GetMainLoop() as SceneTree)?.Root;
            if (searchRoot == null)
                return false;
        }

        Control? best = null;
        float bestScore = float.MinValue;

        foreach (var candidate in EnumerateConfirmCandidates(searchRoot))
        {
            float score = ScoreConfirmCandidate(candidate);
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = candidate;
        }

        if (best == null)
            return false;

        button = best;
        return true;
    }

    private static IEnumerable<Control> EnumerateConfirmCandidates(Node root)
    {
        foreach (var confirm in NodeQuery.FindAll<NConfirmButton>(root))
        {
            if (confirm is Control control && IsUsableConfirm(control))
                yield return control;
        }

        foreach (var confirm in NodeQuery.FindAll<NMiscConfirmButton>(root))
        {
            if (confirm is Control control && IsUsableConfirm(control))
                yield return control;
        }
    }

    private static bool IsUsableConfirm(Control control)
    {
        if (!NodeQuery.IsLive(control) || !NodeQuery.IsVisible(control))
            return false;

        if (control is NClickableControl { IsEnabled: false })
            return false;

        var rect = control.GetGlobalRect();
        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    private static float ScoreConfirmCandidate(Control control)
    {
        var rect = control.GetGlobalRect();
        float score = rect.Size.X * rect.Size.Y;
        string name = control.Name;

        if (name.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
            score += 10000f;

        score += rect.Position.Y * 0.1f;
        return score;
    }

    private static bool TryGetDwellRect(Control control, out Rect2 rect)
    {
        if (ControlHitboxService.TryGetDwellRect(control, out rect))
        {
            rect = rect.Grow(ConfirmPadding);
            return true;
        }

        rect = control.GetGlobalRect().Grow(ConfirmPadding);
        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    private static void ActivateConfirm(Control button)
    {
        if (!NodeQuery.IsLive(button))
        {
            ModLogger.Warn("[CardConfirm] confirm button not live.");
            return;
        }

        if (InputForwardService.TryActivateControl(button))
            ModLogger.Info($"[CardConfirm] '{button.Name}' activated.");
        else
            ModLogger.Warn($"[CardConfirm] '{button.Name}' activation failed.");
    }
}
