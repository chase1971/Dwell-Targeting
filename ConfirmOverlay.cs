using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Optional E / Accept overlay during hand selection (discard, upgrade pick, etc.), plus native
/// game Confirm button dwell whether or not the overlay is shown.
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
    private static int _lastAppliedFontSize = -1;
    private static float _lastAppliedOpacity = -1f;
    private static Control? _cachedPreviewAnchor;
    private static Rect2 _cachedPreviewRect;
    private static Vector2 _cachedPreviewPos;
    private static int _buttonLayoutSizeHash;
    private static int _framesSincePreviewScan;
    private static bool _previewScanExhausted;

    private static NCombatUi? _cachedCombatUi;
    private static Control? _cachedNativeButton;

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
        if (_button == null || !NodeQuery.IsLive(_button) || _buttonBounds.Size.X < 1)
            return null;

        return DwellHoverService.Menu(_buttonBounds, PressConfirmCore, "Confirm");
    }

    internal static void CollectNativeDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (OverlayModeService.GetMode() != OverlayMode.HandSelect)
            return;

        if (!RunManager.Instance.IsInProgress || !CombatManager.Instance.IsInProgress)
            return;

        if (!TryGetNativeConfirmButton(out var button))
            return;

        if (!ControlHitboxService.TryGetDwellRect(button, out var rect))
            return;

        targets.Add(DwellHoverService.Menu(rect, PressConfirmCore, "NativeConfirm"));
    }

    internal static bool TryActivateAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        if (_button != null && NodeQuery.IsLive(_button) && _button.Visible && _buttonBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(PressConfirmCore))
                return false;

            message = "Confirm button clicked";
            return true;
        }

        if (!NativeContainsPoint(globalPos))
            return false;

        if (!DwellActivationCooldown.TryRunMenuAction(PressConfirmCore))
            return false;

        message = "Native Confirm clicked";
        return true;
    }

    internal static void Hide()
    {
        if (_button != null && NodeQuery.IsLive(_button))
            _button.Visible = false;
        _buttonBounds = new Rect2(0, 0, 0, 0);
        _cachedPreviewAnchor = null;
        _buttonLayoutSizeHash = 0;
        _framesSincePreviewScan = 0;
        _previewScanExhausted = false;
        _cachedCombatUi = null;
        _cachedNativeButton = null;
    }

    /// <summary>
    /// One tree search when hand-select starts (discard, upgrade pick, etc.). No periodic rescan.
    /// </summary>
    internal static void RefreshNativeConfirmCache()
    {
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

        if (_cachedCombatUi != null)
            _cachedNativeButton = FindBestConfirmButton(_cachedCombatUi);

        if (_cachedNativeButton == null)
            _cachedNativeButton = FindBestConfirmButton(tree.Root);

        if (_cachedNativeButton != null)
            ModLogger.Info($"Native confirm cached: '{_cachedNativeButton.Name}'.");
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
            () => DwellActivationCooldown.TryRunMenuAction(PressConfirmCore));

        _layer.AddChild(_button);
        InvalidateStyleCache();
        ModLogger.Info("Confirm overlay button created.");
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
            OverlayButtonFactory.ApplyMenuStyle(_button, ConfirmBg, ConfirmBorder, fontSize, opacity);
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

        float x;
        float y;
        bool hasPreview = TryGetPreviewCardRect(out Rect2 previewRect);
        if (hasPreview)
        {
            x = previewRect.Position.X - size.X - 48f;
            y = previewRect.Position.Y + ((previewRect.Size.Y - size.Y) / 2f);
        }
        else
        {
            x = (viewport.Size.X * 0.5f) - size.X - 180f;
            y = viewport.Size.Y * 0.55f;
        }

        int layoutHash = HashCode.Combine(
            (int)x,
            (int)y,
            (int)size.X,
            (int)size.Y,
            hasPreview ? (int)previewRect.Position.X : 0,
            hasPreview ? (int)previewRect.Position.Y : 0);
        if (layoutHash == _buttonLayoutSizeHash)
            return;

        _buttonLayoutSizeHash = layoutHash;
        _button.GlobalPosition = new Vector2(x, y);
        _button.Size = size;
        _buttonBounds = _button.GetGlobalRect();
    }

    private static bool TryGetPreviewCardRect(out Rect2 rect)
    {
        rect = default;

        // Hand-select screens (draw-pile pick, discard, etc.) — skip full-tree NCard scans; they
        // were a major FPS sink and the preview anchor is irrelevant there.
        if (OverlayModeService.GetMode() == OverlayMode.HandSelect)
            return false;

        if (_cachedPreviewAnchor != null
            && NodeQuery.IsLive(_cachedPreviewAnchor)
            && NodeQuery.IsVisible(_cachedPreviewAnchor))
        {
            var pos = _cachedPreviewAnchor.GlobalPosition;
            if (pos.DistanceSquaredTo(_cachedPreviewPos) < 0.25f)
            {
                rect = _cachedPreviewRect;
                return rect.Size.X >= 80f && rect.Size.Y >= 100f;
            }

            rect = _cachedPreviewAnchor.GetGlobalRect();
            if (rect.Size.X >= 80f && rect.Size.Y >= 100f)
            {
                _cachedPreviewRect = rect;
                _cachedPreviewPos = pos;
                return true;
            }
        }

        _framesSincePreviewScan++;
        if (_cachedPreviewRect.Size.X >= 80f
            && _cachedPreviewRect.Size.Y >= 100f)
        {
            rect = _cachedPreviewRect;
            return true;
        }

        if (_previewScanExhausted)
            return false;

        _previewScanExhausted = true;
        _framesSincePreviewScan = 0;
        if (!TryScanPreviewCardRect(out rect))
            return false;

        return true;
    }

    private static bool TryScanPreviewCardRect(out Rect2 rect)
    {
        rect = default;
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        foreach (var container in NodeQuery.FindAll<NSelectedHandCardContainer>(tree.Root))
        {
            if (container is not Control containerControl || !NodeQuery.IsVisible(containerControl))
                continue;

            rect = containerControl.GetGlobalRect();
            if (rect.Size.X >= 80f && rect.Size.Y >= 100f)
            {
                CachePreviewAnchor(containerControl, rect);
                return true;
            }
        }

        var viewport = _button?.GetViewportRect() ?? new Rect2(0, 0, 1920, 1080);
        float minCenterX = viewport.Size.X * 0.3f;
        float maxCenterX = viewport.Size.X * 0.7f;
        float maxCenterY = viewport.Size.Y * 0.55f;

        foreach (var card in NodeQuery.FindAll<NCard>(tree.Root))
        {
            if (card is not Control cardControl || !NodeQuery.IsVisible(cardControl))
                continue;

            rect = cardControl.GetGlobalRect();
            if (rect.Size.X < 120f || rect.Size.Y < 160f)
                continue;

            var center = rect.GetCenter();
            if (center.X >= minCenterX && center.X <= maxCenterX && center.Y <= maxCenterY)
            {
                CachePreviewAnchor(cardControl, rect);
                return true;
            }
        }

        _cachedPreviewAnchor = null;
        return false;
    }

    private static void CachePreviewAnchor(Control anchor, Rect2 rect)
    {
        _cachedPreviewAnchor = anchor;
        _cachedPreviewRect = rect;
        _cachedPreviewPos = anchor.GlobalPosition;
    }

    private static void PressConfirmCore()
    {
        if (TryActivateNativeConfirm())
            return;

        InputForwardService.PressAcceptKey();
    }

    private static bool TryActivateNativeConfirm()
    {
        if (!TryGetNativeConfirmButton(out var button))
            return false;

        if (InputForwardService.TryActivateControl(button))
        {
            ModLogger.Info($"Confirm via native '{button.Name}'.");
            return true;
        }

        return false;
    }

    private static bool NativeContainsPoint(Vector2 globalPos)
    {
        if (!TryGetNativeConfirmButton(out var button))
            return false;

        if (ControlHitboxService.TryGetDwellRect(button, out var rect))
            return rect.HasPoint(globalPos);

        return button.GetGlobalRect().HasPoint(globalPos);
    }

    private static bool TryGetNativeConfirmButton(out Control button)
    {
        button = null!;

        if (_cachedNativeButton == null || !NodeQuery.IsLive(_cachedNativeButton))
            return false;

        if (!NodeQuery.IsVisible(_cachedNativeButton))
            return false;

        if (_cachedNativeButton is NClickableControl { IsEnabled: false })
            return false;

        button = _cachedNativeButton;
        return true;
    }

    private static Control? FindBestConfirmButton(Node root)
    {
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

        return best;
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

        if (name.Contains("SelectMode", StringComparison.OrdinalIgnoreCase))
            score += 20000f;
        else if (name.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
            score += 10000f;

        score += rect.Position.Y * 0.1f;
        return score;
    }
}
