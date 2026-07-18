using Godot;

namespace DwellTargeting;

/// <summary>
/// In-game settings panel for Dwell Targeting (SET button + F8). No external framework required.
/// </summary>
internal static class SettingsOverlay
{
    private const int GearSize = 72;
    private const int PanelWidth = 480;
    private const int CanvasLayerOrder = 131;

    private static CanvasLayer? _layer;
    private static Button? _gearButton;
    private static PanelContainer? _panel;
    private static CheckBox? _hideEndTurnToggle;
    private static Button? _closeButton;
    private static Label? _cardDwellValueLabel;
    private static Label? _endTurnDwellValueLabel;
    private static Label? _menuDwellValueLabel;

    private static Rect2 _gearBounds;
    private static Rect2 _hideEndTurnBounds;
    private static Rect2 _closeBounds;
    private static Rect2 _cardDwellMinusBounds;
    private static Rect2 _cardDwellPlusBounds;
    private static Rect2 _endTurnDwellMinusBounds;
    private static Rect2 _endTurnDwellPlusBounds;
    private static Rect2 _menuDwellMinusBounds;
    private static Rect2 _menuDwellPlusBounds;

    private static bool _open;
    private static bool _initialized;
    private static bool _uiReady;
    private static bool _uiCreateScheduled;
    private static bool _f8WasDown;

    internal static bool IsOpen => _open;

    internal static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        ScheduleUiCreation();
        ModLogger.Info("In-game settings armed — SET (top-left) or F8 once the scene is loaded.");
    }

    internal static void UpdateFrame()
    {
        ProcessFrameInput();

        if (!_uiReady)
        {
            if (!_uiCreateScheduled)
                ScheduleUiCreation();
            return;
        }

        SyncVisibility();
        UpdateBounds();
    }

    internal static void ProcessFrameInput()
    {
        if (ModConfigBridge.IsRegistered)
            return;

        bool f8Down = Input.IsKeyPressed(Key.F8);
        if (f8Down && !_f8WasDown)
            Toggle();

        _f8WasDown = f8Down;
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (!_uiReady)
            return;

        if (_open)
        {
            if (_hideEndTurnBounds.Size.X >= 1)
            {
                targets.Add(DwellHoverService.Menu(
                    _hideEndTurnBounds,
                    ToggleHideEndTurn,
                    "SettingsHideEndTurn"));
            }

            if (_closeBounds.Size.X >= 1)
            {
                targets.Add(DwellHoverService.Menu(
                    _closeBounds,
                    Close,
                    "SettingsClose"));
            }

            AddAdjustDwellTargets(targets);

            return;
        }

        TryAddGearTarget(targets);
    }

    internal static void TryAddGearTarget(List<DwellHoverService.Target> targets)
    {
        if (ModConfigBridge.IsRegistered || _gearBounds.Size.X < 1)
            return;

        targets.Add(DwellHoverService.Menu(
            _gearBounds,
            Open,
            "SettingsGear"));
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        if (!_uiReady)
            return false;

        if (_open)
        {
            if (_closeBounds.Size.X >= 1 && _closeBounds.HasPoint(globalPos))
            {
                if (!DwellActivationCooldown.TryRunMenuAction(Close))
                    return false;

                message = "Settings close clicked";
                return true;
            }

            if (_hideEndTurnBounds.Size.X >= 1 && _hideEndTurnBounds.HasPoint(globalPos))
            {
                if (!DwellActivationCooldown.TryRunMenuAction(ToggleHideEndTurn))
                    return false;

                message = "Settings hide End Turn clicked";
                return true;
            }

            if (TryRouteAdjustClick(globalPos, out message))
                return true;

            if (_panel != null && NodeQuery.IsLive(_panel) && _panel.Visible)
            {
                var panelRect = _panel.GetGlobalRect();
                if (panelRect.HasPoint(globalPos))
                    return true;
            }

            return false;
        }

        if (_gearBounds.Size.X >= 1 && _gearBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(Open))
                return false;

            message = "Settings gear clicked";
            return true;
        }

        return false;
    }

    internal static bool BlocksUnderlyingInput(Vector2 globalPos)
    {
        if (!_uiReady || !_open || _panel == null || !NodeQuery.IsLive(_panel) || !_panel.Visible)
            return false;

        return _panel.GetGlobalRect().HasPoint(globalPos);
    }

    private static void ScheduleUiCreation()
    {
        if (_uiCreateScheduled || _uiReady)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
            return;

        _uiCreateScheduled = true;
        var timer = tree.CreateTimer(0.0);
        timer.Timeout += TryCreateUi;
    }

    private static void TryCreateUi()
    {
        _uiCreateScheduled = false;

        if (_uiReady)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null || !tree.Root.IsInsideTree())
        {
            ScheduleUiCreation();
            return;
        }

        try
        {
            CreateUi(tree.Root);
            if (_layer == null || !_layer.IsInsideTree())
            {
                ModLogger.Warn("Settings UI create finished but layer is not in the scene tree.");
                ScheduleUiCreation();
                return;
            }

            _uiReady = true;
            OverlayCanvasHost.EnsureInputRouter();
            SyncVisibility();
            UpdateBounds();
            ModLogger.Info("In-game settings UI attached — SET top-left or F8.");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Settings UI create failed: {ex.Message}");
            ScheduleUiCreation();
        }
    }

    private static void Toggle()
    {
        if (!_uiReady)
        {
            ModLogger.Info("Settings toggle ignored — UI not ready yet.");
            return;
        }

        if (_open)
            Close();
        else
            Open();
    }

    private static void Open()
    {
        _open = true;
        SyncFromStore();
        SyncVisibility();
        UpdateBounds();
        DwellHoverService.Reset();
        ModLogger.Info("Settings panel opened.");
    }

    private static void Close()
    {
        _open = false;
        SyncVisibility();
        UpdateBounds();
        DwellHoverService.Reset();
        ModLogger.Info("Settings panel closed.");
    }

    private static void ToggleHideEndTurn()
    {
        SettingsStore.SetHideEndTurnButton(!SettingsStore.Current.HideEndTurnButton);
        SyncFromStore();
    }

    private static void AdjustCardDwell(float delta)
    {
        SettingsStore.ApplyCardDwellSeconds(SettingsStore.Current.CardDwellSeconds + delta);
        SyncFromStore();
    }

    private static void AdjustEndTurnDwell(float delta)
    {
        SettingsStore.ApplyEndTurnDwellSeconds(SettingsStore.Current.EndTurnDwellSeconds + delta);
        SyncFromStore();
    }

    private static void AdjustMenuDwell(float delta)
    {
        SettingsStore.ApplyMenuDwellSeconds(SettingsStore.Current.MenuDwellSeconds + delta);
        SyncFromStore();
    }

    private static void SyncFromStore()
    {
        if (_hideEndTurnToggle != null && NodeQuery.IsLive(_hideEndTurnToggle))
            _hideEndTurnToggle.ButtonPressed = SettingsStore.Current.HideEndTurnButton;

        if (_cardDwellValueLabel != null && NodeQuery.IsLive(_cardDwellValueLabel))
            _cardDwellValueLabel.Text = $"{SettingsStore.GetCardDwellSeconds():F2}s";

        if (_endTurnDwellValueLabel != null && NodeQuery.IsLive(_endTurnDwellValueLabel))
            _endTurnDwellValueLabel.Text = $"{SettingsStore.GetEndTurnDwellSeconds():F2}s";

        if (_menuDwellValueLabel != null && NodeQuery.IsLive(_menuDwellValueLabel))
            _menuDwellValueLabel.Text = $"{SettingsStore.GetMenuDwellSeconds():F2}s";
    }

    private static void SyncVisibility()
    {
        if (_gearButton != null && NodeQuery.IsLive(_gearButton))
            _gearButton.Visible = !ModConfigBridge.IsRegistered && !_open;

        if (_panel != null && NodeQuery.IsLive(_panel))
            _panel.Visible = _open;
    }

    private static void UpdateBounds()
    {
        if (_gearButton != null && NodeQuery.IsLive(_gearButton) && _gearButton.Visible)
            _gearBounds = _gearButton.GetGlobalRect();
        else
            _gearBounds = default;

        if (_hideEndTurnToggle != null && NodeQuery.IsLive(_hideEndTurnToggle) && _hideEndTurnToggle.Visible)
            _hideEndTurnBounds = _hideEndTurnToggle.GetGlobalRect();
        else
            _hideEndTurnBounds = default;

        if (_closeButton != null && NodeQuery.IsLive(_closeButton) && _closeButton.Visible)
            _closeBounds = _closeButton.GetGlobalRect();
        else
            _closeBounds = default;

        UpdateAdjustBounds();
    }

    private static void UpdateAdjustBounds()
    {
        _cardDwellMinusBounds = default;
        _cardDwellPlusBounds = default;
        _endTurnDwellMinusBounds = default;
        _endTurnDwellPlusBounds = default;
        _menuDwellMinusBounds = default;
        _menuDwellPlusBounds = default;

        if (_panel == null || !NodeQuery.IsLive(_panel) || !_panel.Visible)
            return;

        foreach (var button in NodeQuery.FindAll<Button>(_panel))
        {
            if (!NodeQuery.IsVisible(button))
                continue;

            switch (button.Name)
            {
                case "CardDwellMinus":
                    _cardDwellMinusBounds = button.GetGlobalRect();
                    break;
                case "CardDwellPlus":
                    _cardDwellPlusBounds = button.GetGlobalRect();
                    break;
                case "EndTurnDwellMinus":
                    _endTurnDwellMinusBounds = button.GetGlobalRect();
                    break;
                case "EndTurnDwellPlus":
                    _endTurnDwellPlusBounds = button.GetGlobalRect();
                    break;
                case "MenuDwellMinus":
                    _menuDwellMinusBounds = button.GetGlobalRect();
                    break;
                case "MenuDwellPlus":
                    _menuDwellPlusBounds = button.GetGlobalRect();
                    break;
            }
        }
    }

    private static void AddAdjustDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_cardDwellMinusBounds.Size.X >= 1)
        {
            targets.Add(DwellHoverService.Menu(
                _cardDwellMinusBounds,
                () => AdjustCardDwell(-0.05f),
                "SettingsCardDwellMinus"));
        }

        if (_cardDwellPlusBounds.Size.X >= 1)
        {
            targets.Add(DwellHoverService.Menu(
                _cardDwellPlusBounds,
                () => AdjustCardDwell(0.05f),
                "SettingsCardDwellPlus"));
        }

        if (_endTurnDwellMinusBounds.Size.X >= 1)
        {
            targets.Add(DwellHoverService.Menu(
                _endTurnDwellMinusBounds,
                () => AdjustEndTurnDwell(-0.05f),
                "SettingsEndTurnDwellMinus"));
        }

        if (_endTurnDwellPlusBounds.Size.X >= 1)
        {
            targets.Add(DwellHoverService.Menu(
                _endTurnDwellPlusBounds,
                () => AdjustEndTurnDwell(0.05f),
                "SettingsEndTurnDwellPlus"));
        }

        if (_menuDwellMinusBounds.Size.X >= 1)
        {
            targets.Add(DwellHoverService.Menu(
                _menuDwellMinusBounds,
                () => AdjustMenuDwell(-0.05f),
                "SettingsMenuDwellMinus"));
        }

        if (_menuDwellPlusBounds.Size.X >= 1)
        {
            targets.Add(DwellHoverService.Menu(
                _menuDwellPlusBounds,
                () => AdjustMenuDwell(0.05f),
                "SettingsMenuDwellPlus"));
        }
    }

    private static bool TryRouteAdjustClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        if (_cardDwellMinusBounds.Size.X >= 1 && _cardDwellMinusBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(() => AdjustCardDwell(-0.05f)))
                return false;

            message = "Card dwell minus";
            return true;
        }

        if (_cardDwellPlusBounds.Size.X >= 1 && _cardDwellPlusBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(() => AdjustCardDwell(0.05f)))
                return false;

            message = "Card dwell plus";
            return true;
        }

        if (_endTurnDwellMinusBounds.Size.X >= 1 && _endTurnDwellMinusBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(() => AdjustEndTurnDwell(-0.05f)))
                return false;

            message = "End Turn dwell minus";
            return true;
        }

        if (_endTurnDwellPlusBounds.Size.X >= 1 && _endTurnDwellPlusBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(() => AdjustEndTurnDwell(0.05f)))
                return false;

            message = "End Turn dwell plus";
            return true;
        }

        if (_menuDwellMinusBounds.Size.X >= 1 && _menuDwellMinusBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(() => AdjustMenuDwell(-0.05f)))
                return false;

            message = "Menu dwell minus";
            return true;
        }

        if (_menuDwellPlusBounds.Size.X >= 1 && _menuDwellPlusBounds.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(() => AdjustMenuDwell(0.05f)))
                return false;

            message = "Menu dwell plus";
            return true;
        }

        return false;
    }

    private static void CreateUi(Node sceneRoot)
    {
        if (_uiReady)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellSettingsLayer" };
        sceneRoot.AddChild(_layer);

        _gearButton = new Button
        {
            Name = "DwellSettingsGear",
            Text = "SET",
            CustomMinimumSize = new Vector2(GearSize, GearSize),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        ApplyButtonStyle(_gearButton, accent: false);
        _gearButton.AddThemeFontSizeOverride("font_size", 20);
        _gearButton.Pressed += Open;
        _layer.AddChild(_gearButton);

        _panel = new PanelContainer
        {
            Name = "DwellSettingsPanel",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        _layer.AddChild(_panel);

        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 18);
        _panel.AddChild(root);

        var title = new Label
        {
            Text = "Dwell Targeting",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        root.AddChild(title);

        var hint = new Label
        {
            Text = "Press F8 to open or close this panel.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(PanelWidth - 48, 0)
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        root.AddChild(hint);

        root.AddChild(CreateDwellAdjustRow(
            "Card hover time",
            "CardDwell",
            () => AdjustCardDwell(-0.05f),
            () => AdjustCardDwell(0.05f),
            out _cardDwellValueLabel));

        root.AddChild(CreateDwellAdjustRow(
            "End Turn hover time",
            "EndTurnDwell",
            () => AdjustEndTurnDwell(-0.05f),
            () => AdjustEndTurnDwell(0.05f),
            out _endTurnDwellValueLabel));

        root.AddChild(CreateDwellAdjustRow(
            "Menu & utility hover time",
            "MenuDwell",
            () => AdjustMenuDwell(-0.05f),
            () => AdjustMenuDwell(0.05f),
            out _menuDwellValueLabel));

        _hideEndTurnToggle = new CheckBox
        {
            Text = "Hide center End Turn overlay (native button dwell still works)",
            CustomMinimumSize = new Vector2(PanelWidth - 48, 64),
            FocusMode = Control.FocusModeEnum.None
        };
        _hideEndTurnToggle.AddThemeFontSizeOverride("font_size", 20);
        _hideEndTurnToggle.Toggled += pressed => SettingsStore.SetHideEndTurnButton(pressed);
        root.AddChild(_hideEndTurnToggle);

        _closeButton = new Button
        {
            Text = "CLOSE",
            CustomMinimumSize = new Vector2(220, 64),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        ApplyButtonStyle(_closeButton, accent: true);
        _closeButton.AddThemeFontSizeOverride("font_size", 22);
        _closeButton.Pressed += Close;
        root.AddChild(_closeButton);

        SyncFromStore();
        PositionUi();
    }

    private static Control CreateDwellAdjustRow(
        string title,
        string namePrefix,
        Action onMinus,
        Action onPlus,
        out Label valueLabel)
    {
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        row.AddChild(titleLabel);

        var controls = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        controls.AddThemeConstantOverride("separation", 16);
        row.AddChild(controls);

        var minus = new Button
        {
            Name = $"{namePrefix}Minus",
            Text = "−",
            CustomMinimumSize = new Vector2(72, 64),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        ApplyButtonStyle(minus, accent: false);
        minus.AddThemeFontSizeOverride("font_size", 32);
        minus.Pressed += onMinus;
        controls.AddChild(minus);

        valueLabel = new Label
        {
            Text = "0.00s",
            CustomMinimumSize = new Vector2(120, 64),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 24);
        controls.AddChild(valueLabel);

        var plus = new Button
        {
            Name = $"{namePrefix}Plus",
            Text = "+",
            CustomMinimumSize = new Vector2(72, 64),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        ApplyButtonStyle(plus, accent: false);
        plus.AddThemeFontSizeOverride("font_size", 32);
        plus.Pressed += onPlus;
        controls.AddChild(plus);

        return row;
    }

    private static void PositionUi()
    {
        if (_gearButton != null && NodeQuery.IsLive(_gearButton) && _gearButton.IsInsideTree())
        {
            _gearButton.GlobalPosition = new Vector2(20, 20);
            _gearButton.ResetSize();
            _gearButton.Size = _gearButton.GetCombinedMinimumSize();
        }

        if (_panel != null && NodeQuery.IsLive(_panel) && _panel.IsInsideTree())
        {
            var viewport = _panel.GetViewportRect();
            _panel.ResetSize();
            var size = _panel.GetCombinedMinimumSize();
            _panel.GlobalPosition = new Vector2(
                (viewport.Size.X - size.X) * 0.5f,
                (viewport.Size.Y - size.Y) * 0.42f);
            _panel.Size = size;
        }
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.1f, 0.12f, 0.96f),
            BorderColor = new Color(0.45f, 0.75f, 0.95f, 1f),
            BorderWidthBottom = 3,
            BorderWidthTop = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 24,
            ContentMarginBottom = 24
        };
    }

    private static void ApplyButtonStyle(Button button, bool accent)
    {
        var style = new StyleBoxFlat
        {
            BgColor = accent
                ? new Color(0.12f, 0.22f, 0.32f, 0.95f)
                : new Color(0.14f, 0.16f, 0.18f, 0.92f),
            BorderColor = accent
                ? new Color(0.45f, 0.85f, 1f, 1f)
                : new Color(0.55f, 0.65f, 0.75f, 1f),
            BorderWidthBottom = 3,
            BorderWidthTop = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("focus", style);
    }
}
