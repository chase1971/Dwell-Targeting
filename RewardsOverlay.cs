using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

/// <summary>
/// Numbered dwell buttons beside loot/reward choices, plus a direct native dwell on the Skip/Proceed
/// button (same method as the combat utility bar — hover the real button, ForceClick to activate).
/// </summary>
internal static class RewardsOverlay
{
    private const int ButtonSize = 44;
    private const int CanvasLayerOrder = 130;
    private const int SideOffset = 72;
    private const float ProceedHitboxPadding = 24f;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly Dictionary<ulong, RewardSideButton> _sideButtons = new();
    private static NProceedButton? _proceedButton;
    private static long _nextProceedDiagTick;
    private static bool _lastInsideProceed;

    internal static void Sync()
    {
        var rewardsScreen = OverlayModeService.GetCachedRewardsScreen();
        if (rewardsScreen == null)
        {
            Hide();
            return;
        }

        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;

        var rewardButtons = RewardsScreenQuery.GetRewardButtons(rewardsScreen);
        var liveIds = new HashSet<ulong>();
        int slot = 1;
        foreach (var reward in rewardButtons)
        {
            ulong id = reward.GetInstanceId();
            liveIds.Add(id);

            if (!_sideButtons.TryGetValue(id, out var side))
            {
                side = RewardSideButton.ForReward(reward, _root);
                _sideButtons[id] = side;
            }

            side.Sync(slot, ButtonSize);
            slot++;
        }

        _proceedButton = RewardsScreenQuery.GetProceedButton(rewardsScreen);

        foreach (var pair in _sideButtons.ToList())
        {
            if (!liveIds.Contains(pair.Key))
            {
                pair.Value.Dispose();
                _sideButtons.Remove(pair.Key);
            }
        }

        LogProceedDiagnostic();
    }

    /// <summary>
    /// Throttled trace of the Proceed/Skip button state + whether the cursor is inside its dwell rect,
    /// so we can see exactly what (if anything) happens when the mouse hovers it.
    /// </summary>
    private static void LogProceedDiagnostic()
    {
        long now = System.Environment.TickCount64;

        if (_proceedButton == null)
        {
            if (now < _nextProceedDiagTick)
                return;
            _nextProceedDiagTick = now + 1000;
            ModLogger.Info("[ProceedDiag] proceed button = null (none found on this rewards screen).");
            _lastInsideProceed = false;
            return;
        }

        bool live = NodeQuery.IsLive(_proceedButton);
        bool visible = live && NodeQuery.IsVisible(_proceedButton);
        bool enabled = _proceedButton is not NClickableControl { IsEnabled: false };
        bool hasRect = TryGetProceedRect(out var rect);
        var mouse = DwellHoverService.GetMousePosition();
        bool inside = hasRect && mouse != null && rect.HasPoint(mouse.Value);

        // Always log the moment the cursor enters/leaves the proceed rect; otherwise throttle to ~1s.
        bool edge = inside != _lastInsideProceed;
        _lastInsideProceed = inside;
        if (!edge && now < _nextProceedDiagTick)
            return;
        _nextProceedDiagTick = now + 1000;

        ModLogger.Info(
            $"[ProceedDiag] name={_proceedButton.Name} live={live} visible={visible} enabled={enabled} " +
            $"hasRect={hasRect} rect=({rect.Position.X:F0},{rect.Position.Y:F0},{rect.Size.X:F0}x{rect.Size.Y:F0}) " +
            $"mouse=({(mouse?.X ?? -1f):F0},{(mouse?.Y ?? -1f):F0}) insideProceed={inside} " +
            $"menuCooldown={DwellActivationCooldown.IsMenuBlocked} sideButtons={_sideButtons.Count}");
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        foreach (var side in _sideButtons.Values)
            side.CollectDwellTargets(targets);

        if (TryGetProceedRect(out var rect))
        {
            targets.Add(DwellHoverService.Menu(
                rect,
                ActivateProceed,
                "NativeProceed:Skip"));
        }
    }

    internal static void Hide()
    {
        foreach (var side in _sideButtons.Values)
            side.Dispose();
        _sideButtons.Clear();
        _proceedButton = null;

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var side in _sideButtons.Values)
        {
            if (side.TryActivateAt(globalPos, out message))
                return true;
        }

        if (TryGetProceedRect(out var rect) && rect.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(ActivateProceed))
                return false;

            message = "Native proceed clicked";
            return true;
        }

        return false;
    }

    internal static bool TryHitDwellButton(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var side in _sideButtons.Values)
        {
            if (side.TryHitAt(globalPos, out message))
                return true;
        }

        if (TryGetProceedRect(out var rect) && rect.HasPoint(globalPos))
        {
            message = "Hit native proceed";
            return true;
        }

        return false;
    }

    internal static bool ContainsPoint(Vector2 globalPos)
    {
        foreach (var side in _sideButtons.Values)
        {
            if (side.ContainsPoint(globalPos))
                return true;
        }

        return TryGetProceedRect(out var rect) && rect.HasPoint(globalPos);
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

        // The button has a hover/pulse animation (~±7 px) and the head-mouse wobbles; pad the dwell
        // hitbox so the cursor stays "inside" instead of flickering across the edge.
        rect = rect.Grow(ProceedHitboxPadding);
        return true;
    }

    private static void ActivateProceed()
    {
        if (_proceedButton == null || !NodeQuery.IsLive(_proceedButton))
        {
            ModLogger.Warn("[ProceedDiag] ActivateProceed fired but proceed button missing/dead.");
            return;
        }

        ModLogger.Info($"[ProceedDiag] ActivateProceed firing on '{_proceedButton.Name}'.");
        RewardSelectionService.TryProceed(_proceedButton);
    }

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellRewardsLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellRewardsRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);
    }

    private sealed class RewardSideButton
    {
        private readonly NRewardButton? _rewardTarget;
        private readonly Control _host;
        private Button? _button;
        private string _label = "?";

        private RewardSideButton(NRewardButton reward, Control root)
        {
            _rewardTarget = reward;
            _host = new Control
            {
                Name = $"DwellRewardSide_{reward.GetInstanceId()}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 200
            };
            root.AddChild(_host);
        }

        internal static RewardSideButton ForReward(NRewardButton reward, Control root) =>
            new(reward, root);

        internal void Sync(int slot, int buttonSize)
        {
            _label = slot.ToString();
            EnsureButton(buttonSize);
            PositionButton();
        }

        internal void CollectDwellTargets(List<DwellHoverService.Target> targets)
        {
            if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
                return;

            targets.Add(DwellHoverService.Card(_button.GetGlobalRect(), Activate, $"Reward:{_label}"));

            // Also let dwelling directly on the loot item itself select it. These items have no
            // keybind, so we route the hover to the same proven claim path the number button uses.
            if (_rewardTarget != null && NodeQuery.IsLive(_rewardTarget) && NodeQuery.IsVisible(_rewardTarget)
                && ControlHitboxService.TryGetDwellRect(_rewardTarget, out var itemRect))
            {
                targets.Add(DwellHoverService.Card(itemRect, Activate, $"RewardItem:{_label}"));
            }
        }

        internal bool ContainsPoint(Vector2 globalPos) =>
            _button != null && NodeQuery.IsLive(_button) && _button.Visible && _button.GetGlobalRect().HasPoint(globalPos);

        internal bool TryHitAt(Vector2 globalPos, out string message) =>
            TryActivateAt(globalPos, out message, activate: false);

        internal bool TryActivateAt(Vector2 globalPos, out string message) =>
            TryActivateAt(globalPos, out message, activate: true);

        private bool TryActivateAt(Vector2 globalPos, out string message, bool activate)
        {
            message = string.Empty;
            if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
                return false;

            if (!_button.GetGlobalRect().HasPoint(globalPos))
                return false;

            message = activate ? $"Reward side '{_label}' activated" : $"Hit reward side '{_label}'";
            if (activate)
                Activate();

            return true;
        }

        private void Activate()
        {
            if (_rewardTarget != null && NodeQuery.IsLive(_rewardTarget))
                RewardSelectionService.TryClaim(_rewardTarget);
        }

        internal void Dispose()
        {
            if (NodeQuery.IsLive(_host))
                _host.QueueFree();
            _button = null;
        }

        private void EnsureButton(int buttonSize)
        {
            if (_button != null && NodeQuery.IsLive(_button))
            {
                _button.Text = _label;
                _button.CustomMinimumSize = new Vector2(buttonSize, buttonSize);
                _button.Visible = true;
                return;
            }

            int fontSize = Math.Max(12, buttonSize / 2);
            _button = new Button
            {
                Text = _label,
                CustomMinimumSize = new Vector2(buttonSize, buttonSize),
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 2
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.1f, 0.14f, 0.95f),
                BorderColor = new Color(0.45f, 0.85f, 1f, 1f),
                BorderWidthBottom = 2,
                BorderWidthTop = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8
            };
            _button.AddThemeStyleboxOverride("normal", style);
            _button.AddThemeStyleboxOverride("hover", style);
            _button.AddThemeStyleboxOverride("pressed", style);
            _button.AddThemeStyleboxOverride("focus", style);
            _button.AddThemeFontSizeOverride("font_size", fontSize);
            _button.Pressed += Activate;
            _host.AddChild(_button);
        }

        private void PositionButton()
        {
            Control? anchor = _rewardTarget;
            if (_button == null || anchor == null || !NodeQuery.IsLive(anchor))
                return;

            var targetRect = anchor.GetGlobalRect();
            _button.ResetSize();
            var size = _button.GetCombinedMinimumSize();
            float x = targetRect.Position.X - SideOffset - size.X;
            float y = targetRect.Position.Y + ((targetRect.Size.Y - size.Y) / 2f);
            _button.GlobalPosition = new Vector2(x, y);
            _button.Size = size;
            _button.Visible = true;
        }
    }
}
