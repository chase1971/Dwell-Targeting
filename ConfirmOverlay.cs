using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

/// <summary>
/// E / Accept button during hand selection (discard, upgrade pick, etc.).
/// </summary>
internal static class ConfirmOverlay
{
    private const int CanvasLayerOrder = 129;

    private static readonly Color ConfirmBg = new(0.14f, 0.12f, 0.08f, 0.92f);
    private static readonly Color ConfirmBorder = new(0.95f, 0.85f, 0.45f, 1f);

    private static CanvasLayer? _layer;
    private static Button? _button;
    private static Rect2 _buttonBounds;
    private static int _buttonSize;

    internal static void Sync(bool visible)
    {
        if (!visible || SettingsStore.Current.HideConfirmButton)
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

        return new DwellHoverService.Target(_buttonBounds, PressConfirm, "Confirm");
    }

    internal static bool TryActivateAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
            return false;

        if (!ContainsPoint(globalPos))
            return false;

        message = "Confirm button clicked";
        PressConfirm();
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
            _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellConfirmLayer" };
            tree.Root.AddChild(_layer);
        }

        _buttonSize = SettingsStore.GetActionButtonSize();
        _button = OverlayButtonFactory.CreateMenuButton(
            "DwellConfirmButton",
            "E\nOK",
            _buttonSize,
            ConfirmBg,
            ConfirmBorder,
            PressConfirm);

        _layer.AddChild(_button);
        ModLogger.Info("Confirm overlay button created.");
    }

    private static void ApplyPresentation()
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
        OverlayButtonFactory.ApplyMenuStyle(_button, ConfirmBg, ConfirmBorder, fontSize, SettingsStore.GetMenuButtonOpacity());
    }

    private static void PositionButton()
    {
        if (_button == null)
            return;

        var viewport = _button.GetViewportRect();
        _button.ResetSize();
        var size = _button.GetCombinedMinimumSize();

        float x;
        float y;
        if (TryGetPreviewCardRect(out Rect2 previewRect))
        {
            x = previewRect.Position.X - size.X - 48f;
            y = previewRect.Position.Y + ((previewRect.Size.Y - size.Y) / 2f);
        }
        else
        {
            x = (viewport.Size.X * 0.5f) - size.X - 180f;
            y = viewport.Size.Y * 0.55f;
        }

        _button.GlobalPosition = new Vector2(x, y);
        _button.Size = size;
        _buttonBounds = _button.GetGlobalRect();
    }

    private static bool TryGetPreviewCardRect(out Rect2 rect)
    {
        rect = default;
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        foreach (var container in NodeQuery.FindAll<NSelectedHandCardContainer>(tree.Root))
        {
            if (!NodeQuery.IsVisible(container))
                continue;

            rect = container.GetGlobalRect();
            if (rect.Size.X >= 80f && rect.Size.Y >= 100f)
                return true;
        }

        var viewport = _button?.GetViewportRect() ?? new Rect2(0, 0, 1920, 1080);
        float minCenterX = viewport.Size.X * 0.3f;
        float maxCenterX = viewport.Size.X * 0.7f;
        float maxCenterY = viewport.Size.Y * 0.55f;

        foreach (var card in NodeQuery.FindAll<NCard>(tree.Root))
        {
            if (!NodeQuery.IsVisible(card))
                continue;

            rect = card.GetGlobalRect();
            if (rect.Size.X < 120f || rect.Size.Y < 160f)
                continue;

            var center = rect.GetCenter();
            if (center.X >= minCenterX && center.X <= maxCenterX && center.Y <= maxCenterY)
                return true;
        }

        return false;
    }

    private static void PressConfirm()
    {
        InputForwardService.PressAcceptKey();
    }
}
