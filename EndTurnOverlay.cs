using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Large dwell-friendly End Turn button centered above the hand (open floor area).
/// </summary>
internal static class EndTurnOverlay
{
    private const int CanvasLayerOrder = 129;

    private static readonly Color EndTurnBg = new(0.12f, 0.18f, 0.1f, 0.92f);
    private static readonly Color EndTurnBorder = new(0.55f, 0.95f, 0.45f, 1f);

    private static CanvasLayer? _layer;
    private static Button? _button;
    private static Rect2 _buttonBounds;
    private static int _buttonSize;
    private static int _lastAppliedFontSize = -1;
    private static float _lastAppliedOpacity = -1f;

    internal static void Sync(bool visible)
    {
        if (!visible || SettingsStore.Current.HideEndTurnButton)
        {
            Hide();
            return;
        }

        EnsureButton();
        if (_button == null)
            return;

        ApplyPresentation();
        _button.Visible = true;
        PositionButton();
    }

    internal static bool ContainsPoint(Vector2 globalPos) =>
        _buttonBounds.Size.X >= 1 && _buttonBounds.HasPoint(globalPos);

    internal static bool TryHitAt(Vector2 globalPos)
    {
        if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
            return false;

        return ContainsPoint(globalPos);
    }

    internal static DwellHoverService.Target? GetDwellTarget()
    {
        if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
            return null;

        return DwellHoverService.EndTurn(_buttonBounds, PressEndTurnCore);
    }

    internal static bool TryActivateAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
            return false;

        if (!ContainsPoint(globalPos))
            return false;

        if (!DwellActivationCooldown.TryRunMenuAction(PressEndTurnCore))
            return false;

        message = "EndTurn button clicked";
        return true;
    }

    internal static void Hide()
    {
        if (_button != null && NodeQuery.IsLive(_button))
            _button.Visible = false;
        _buttonBounds = new Rect2(0, 0, 0, 0);
    }

    private static void EnsureButton()
    {
        if (_button != null && NodeQuery.IsLive(_button))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        if (_layer == null || !NodeQuery.IsLive(_layer))
        {
            _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellEndTurnLayer" };
            tree.Root.AddChild(_layer);
        }

        _buttonSize = SettingsStore.GetActionButtonSize();
        _button = OverlayButtonFactory.CreateMenuButton(
            "DwellEndTurnButton",
            "E\nEND",
            _buttonSize,
            EndTurnBg,
            EndTurnBorder,
            () => DwellActivationCooldown.TryRunMenuAction(PressEndTurnCore));

        _layer.AddChild(_button);
        InvalidateStyleCache();
        ModLogger.Info("End Turn overlay button created.");
    }

    private static void ApplyPresentation()
    {
        long tick = OverlayPerfDiagnostics.BeginTick();
        try
        {
            if (_button == null || !NodeQuery.IsLive(_button))
                return;

            int size = SettingsStore.GetActionButtonSize();
            if (size != _buttonSize)
            {
                _buttonSize = size;
                OverlayButtonFactory.ApplySize(_button, size);
            }

            int fontSize = Math.Clamp(_buttonSize / 5, 14, 28);
            float opacity = SettingsStore.GetMenuButtonOpacity();
            if (fontSize == _lastAppliedFontSize && Math.Abs(opacity - _lastAppliedOpacity) < 0.001f)
                return;

            _lastAppliedFontSize = fontSize;
            _lastAppliedOpacity = opacity;
            OverlayButtonFactory.ApplyMenuStyle(_button, EndTurnBg, EndTurnBorder, fontSize, opacity);
        }
        finally
        {
            OverlayPerfDiagnostics.AddCategory("styles", tick);
        }
    }

    private static void InvalidateStyleCache()
    {
        _lastAppliedFontSize = -1;
        _lastAppliedOpacity = -1f;
    }

    private static void PositionButton()
    {
        if (_button == null)
            return;

        var viewport = _button.GetViewportRect();
        float centerX = viewport.Size.X * 0.5f;
        float centerY = viewport.Size.Y * 0.52f;

        _button.ResetSize();
        var size = _button.GetCombinedMinimumSize();
        _button.GlobalPosition = new Vector2(centerX - (size.X / 2f), centerY - (size.Y / 2f));
        _button.Size = size;
        _buttonBounds = _button.GetGlobalRect();
    }

    private static void PressEndTurnCore()
    {
        if (TrySetReadyToEndTurn())
            return;

        if (TryEmitGameEndTurnButton())
            return;

        InputForwardService.PressAcceptKey();
    }

    private static bool TrySetReadyToEndTurn()
    {
        try
        {
            if (!RunManager.Instance.IsInProgress || !CombatManager.Instance.IsInProgress)
                return false;

            var runState = RunManager.Instance.DebugOnlyGetState();
            var player = runState == null ? null : LocalContext.GetMe(runState);
            if (player == null)
                return false;

            CombatManager.Instance.SetReadyToEndTurn(player, true, null);
            ModLogger.Info("EndTurn via CombatManager.SetReadyToEndTurn");
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"EndTurn SetReadyToEndTurn failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryEmitGameEndTurnButton()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var endTurnButton = tree?.Root == null
                ? null
                : NodeQuery.FindAll<NCombatUi>(tree.Root).FirstOrDefault()?.EndTurnButton;
            if (endTurnButton == null || !NodeQuery.IsLive(endTurnButton))
                return false;

            endTurnButton.EmitSignal(Button.SignalName.Pressed);
            ModLogger.Info("EndTurn via EndTurnButton.EmitSignal(Pressed)");
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"EndTurn via UI button failed: {ex.Message}");
            return false;
        }
    }
}
