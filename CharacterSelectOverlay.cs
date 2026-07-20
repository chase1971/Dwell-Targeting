using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Dwell on character icons plus the screen Confirm button during pre-run character selection.
/// </summary>
internal static class CharacterSelectOverlay
{
    private static NCharacterSelectScreen? _screen;
    private static Control? _confirmButton;
    private static List<(Rect2 Bounds, Control Button, int Slot)>? _dwellTargets;
    private static Rect2 _confirmBounds;
    private static ScreenEntryScanState _entryScan;

    internal static bool IsActive()
    {
        if (RunManager.Instance.IsInProgress)
            return false;

        return FindScreen() != null;
    }

    internal static void Sync()
    {
        if (RunManager.Instance.IsInProgress || !IsActive())
        {
            Hide();
            return;
        }

        _screen = FindScreen();
        if (_screen == null)
        {
            Hide();
            return;
        }

        ulong screenId = _screen.GetInstanceId();
        if (!_entryScan.ShouldScan(screenId))
            return;

        RebuildTargets();
        _entryScan.MarkScanned(_dwellTargets?.Count ?? 0, "CharacterSelect");
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_dwellTargets != null)
        {
            foreach (var (_, button, slot) in _dwellTargets)
            {
                if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
                    continue;

                if (!ControlHitboxService.TryGetDwellRect(button, out var rect))
                    rect = button.GetGlobalRect();
                if (rect.Size.X < 8f || rect.Size.Y < 8f)
                    continue;

                var captured = button;
                int capturedSlot = slot;
                targets.Add(DwellHoverService.Menu(
                    rect,
                    () => Activate(captured, capturedSlot),
                    $"CharacterSelect:{capturedSlot}"));
            }
        }

        if (_confirmButton != null
            && NodeQuery.IsLive(_confirmButton) && NodeQuery.IsVisible(_confirmButton)
            && _confirmButton is not NClickableControl { IsEnabled: false })
        {
            if (!ControlHitboxService.TryGetDwellRect(_confirmButton, out var rect))
                rect = _confirmButton.GetGlobalRect();
            if (rect.Size.X >= 8f && rect.Size.Y >= 8f)
            {
                var capturedConfirm = _confirmButton;
                targets.Add(DwellHoverService.Menu(
                    rect,
                    () => Activate(capturedConfirm, 0),
                    "CharacterSelectConfirm"));
            }
        }
    }

    internal static void Hide()
    {
        _screen = null;
        _confirmButton = null;
        _dwellTargets = null;
        _confirmBounds = default;
        _entryScan.OnHide();
    }

    internal static void PrepareForEntry() => _entryScan.ScheduleRescan("CharacterSelect");

    private static void RebuildTargets()
    {
        if (_screen == null || !NodeQuery.IsLive(_screen))
        {
            _dwellTargets = null;
            _confirmButton = null;
            _confirmBounds = default;
            return;
        }

        var buttons = FindCharacterButtons(_screen);
        var targets = new List<(Rect2, Control, int)>();
        for (int i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (!ControlHitboxService.TryGetDwellRect(button, out var rect))
                rect = button.GetGlobalRect();

            if (rect.Size.X < 8f || rect.Size.Y < 8f)
                continue;

            targets.Add((rect, button, i + 1));
        }

        _dwellTargets = targets.Count > 0 ? targets : null;
        _confirmButton = FindConfirmButton(_screen);
        _confirmBounds = default;

        if (_confirmButton != null
            && NodeQuery.IsLive(_confirmButton)
            && NodeQuery.IsVisible(_confirmButton)
            && _confirmButton is not NClickableControl { IsEnabled: false })
        {
            if (ControlHitboxService.TryGetDwellRect(_confirmButton, out var rect))
                _confirmBounds = rect;
            else
                _confirmBounds = _confirmButton.GetGlobalRect();
        }
    }

    private static NCharacterSelectScreen? FindScreen()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var screen in NodeQuery.FindAllVisible<NCharacterSelectScreen>(root))
        {
            if (NodeQuery.IsLive(screen) && NodeQuery.IsVisible(screen))
                return screen;
        }

        return null;
    }

    private static List<Control> FindCharacterButtons(NCharacterSelectScreen screen)
    {
        var list = new List<Control>();
        foreach (var button in NodeQuery.FindAll<NCharacterSelectButton>(screen))
            TryAddButton(list, button);

        list.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
            return cmp != 0 ? cmp : a.GetInstanceId().CompareTo(b.GetInstanceId());
        });
        return list;
    }

    private static Control? FindConfirmButton(NCharacterSelectScreen screen)
    {
        Control? best = null;
        float bestScore = float.MinValue;

        foreach (var confirm in NodeQuery.FindAll<NConfirmButton>(screen))
        {
            if (confirm is not Control control || !IsUsableButton(control))
                continue;

            float score = ScoreConfirmCandidate(control);
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = control;
        }

        foreach (var confirm in NodeQuery.FindAll<NMiscConfirmButton>(screen))
        {
            if (confirm is not Control control || !IsUsableButton(control))
                continue;

            float score = ScoreConfirmCandidate(control);
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = control;
        }

        return best;
    }

    private static float ScoreConfirmCandidate(Control control)
    {
        var rect = control.GetGlobalRect();
        float score = rect.Size.X * rect.Size.Y + rect.Position.Y * 0.1f;
        if (control.Name.ToString().Contains("Confirm", StringComparison.OrdinalIgnoreCase))
            score += 10000f;
        return score;
    }

    private static bool IsUsableButton(Control control)
    {
        if (!NodeQuery.IsLive(control) || !NodeQuery.IsVisible(control))
            return false;
        if (control is NClickableControl { IsEnabled: false })
            return false;

        var rect = control.GetGlobalRect();
        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    private static void TryAddButton(List<Control> list, Control control)
    {
        if (!NodeQuery.IsVisible(control))
            return;
        if (control is NClickableControl { IsEnabled: false })
            return;

        foreach (var existing in list)
        {
            if (existing == control)
                return;
        }

        list.Add(control);
    }

    private static void Activate(Control button, int slot)
    {
        if (!NodeQuery.IsLive(button))
        {
            ModLogger.Warn($"[CharacterSelect] option {slot} not live.");
            return;
        }

        if (InputForwardService.TryActivateControl(button))
            ModLogger.Info($"[CharacterSelect] option {slot} '{button.Name}' activated.");
        else
            ModLogger.Warn($"[CharacterSelect] option {slot} activation failed.");
    }
}
