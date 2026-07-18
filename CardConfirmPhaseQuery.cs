using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DwellTargeting;

/// <summary>
/// Card upgrade / removal / pile-select confirmation: native checkmark + back only — no grid picks.
/// </summary>
internal static class CardConfirmPhaseQuery
{
    private const float ConfirmPadding = 12f;

    internal static bool IsActive()
    {
        if (!TryGetConfirmButton(out _))
            return false;

        var mode = OverlayModeService.GetMode();
        if (mode == OverlayMode.PileSelect)
            return true;

        if (mode == OverlayMode.HandSelect)
            return HasSelectedCardPreview();

        return HasSelectedCardPreview();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (!IsActive() || !TryGetConfirmButton(out var button))
            return;

        if (!TryGetDwellRect(button, out var rect))
            return;

        var captured = button;
        targets.Add(DwellHoverService.Menu(
            rect,
            () => ActivateConfirm(captured),
            "CardConfirm"));
    }

    private static bool HasSelectedCardPreview()
    {
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

    private static bool TryGetConfirmButton(out Control button)
    {
        button = null!;
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return false;

        Control? best = null;
        float bestScore = float.MinValue;

        foreach (var candidate in EnumerateConfirmCandidates(root))
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
