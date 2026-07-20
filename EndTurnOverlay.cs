using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Large dwell-friendly End Turn button centered above the hand (open floor area),
/// plus native End Turn button dwell in the bottom-right.
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

    private static NCombatUi? _cachedCombatUi;
    private static Control? _cachedNativeButton;

    internal static void Sync(bool visible)
    {
        if (!visible || SettingsStore.Current.HideEndTurnButton)
        {
            Hide();
            return;
        }

        if (OverlayModeService.TryGetPileSelectScreen(out _))
        {
            Hide();
            return;
        }

        EnsureNativeEndTurnCached();
        EnsureButton();
        if (_button == null)
            return;

        ApplyPresentation();
        PositionButton();
        if (_button != null && NodeQuery.IsLive(_button))
            _button.Visible = SettingsStore.Current.ShowOverlays;
    }

    internal static bool ContainsPoint(Vector2 globalPos) =>
        (_buttonBounds.Size.X >= 1 && _buttonBounds.HasPoint(globalPos))
        || NativeContainsPoint(globalPos);

    internal static bool TryHitAt(Vector2 globalPos)
    {
        if (_button != null && NodeQuery.IsLive(_button) && _button.Visible && _buttonBounds.HasPoint(globalPos))
            return true;

        return NativeContainsPoint(globalPos);
    }

    internal static DwellHoverService.Target? GetDwellTarget()
    {
        if (OverlayModeService.TryGetPileSelectScreen(out _))
            return null;

        if (_button == null || !NodeQuery.IsLive(_button) || _buttonBounds.Size.X < 1)
            return null;

        return DwellHoverService.EndTurn(_buttonBounds, PressEndTurnCore);
    }

    internal static void CollectNativeDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (OverlayModeService.GetMode() != OverlayMode.CombatPlay)
            return;

        if (OverlayModeService.TryGetPileSelectScreen(out _))
            return;

        if (!RunManager.Instance.IsInProgress || !CombatManager.Instance.IsInProgress)
            return;

        if (!TryGetNativeEndTurnButton(out var button))
            return;

        var rect = button.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return;

        targets.Add(DwellHoverService.EndTurn(rect, PressEndTurnCore));
    }

    internal static bool TryActivateAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        if (_button != null && NodeQuery.IsLive(_button) && _button.Visible && _buttonBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(PressEndTurnCore))
                return false;

            message = "EndTurn button clicked";
            return true;
        }

        if (!NativeContainsPoint(globalPos))
            return false;

        if (!DwellActivationCooldown.TryRunMenuAction(PressEndTurnCore))
            return false;

        message = "Native End Turn clicked";
        return true;
    }

    internal static void Hide()
    {
        if (_button != null && NodeQuery.IsLive(_button))
            _button.Visible = false;
        _buttonBounds = new Rect2(0, 0, 0, 0);
        _cachedCombatUi = null;
        _cachedNativeButton = null;
        EndTurnPlacement.InvalidatePlayerCache();
    }

    internal static void EnsureNativeEndTurnCached()
    {
        if (_cachedNativeButton != null && NodeQuery.IsLive(_cachedNativeButton))
            return;

        _cachedCombatUi = null;
        _cachedNativeButton = null;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        foreach (var ui in NodeQuery.FindAll<NCombatUi>(tree.Root))
        {
            if (!NodeQuery.IsVisible(ui))
                continue;

            _cachedCombatUi = ui;
            break;
        }

        var native = _cachedCombatUi?.EndTurnButton as Control;
        if (native != null && NodeQuery.IsLive(native) && NodeQuery.IsVisible(native))
            _cachedNativeButton = native;
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
        _button.ResetSize();
        var size = _button.GetCombinedMinimumSize();
        var center = EndTurnPlacement.ResolveCenter(viewport.Size, size);
        _button.GlobalPosition = new Vector2(center.X - (size.X / 2f), center.Y - (size.Y / 2f));
        _button.Size = size;
        _buttonBounds = _button.GetGlobalRect();
    }

    private static bool NativeContainsPoint(Vector2 globalPos)
    {
        if (!TryGetNativeEndTurnButton(out var button))
            return false;

        return button.GetGlobalRect().HasPoint(globalPos);
    }

    private static bool TryGetNativeEndTurnButton(out Control button)
    {
        button = null!;
        EnsureNativeEndTurnCached();

        if (_cachedNativeButton == null || !NodeQuery.IsLive(_cachedNativeButton) || !NodeQuery.IsVisible(_cachedNativeButton))
            return false;

        if (_cachedNativeButton is NClickableControl { IsEnabled: false })
            return false;

        button = _cachedNativeButton;
        return true;
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
            EnsureNativeEndTurnCached();
            if (_cachedNativeButton == null || !NodeQuery.IsLive(_cachedNativeButton))
                return false;

            if (_cachedNativeButton is NClickableControl clickable)
            {
                clickable.ForceClick();
                ModLogger.Info("EndTurn via native ForceClick");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"EndTurn via UI button failed: {ex.Message}");
            return false;
        }
    }
}
