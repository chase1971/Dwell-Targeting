using Godot;

namespace DwellTargeting;

internal static class OverlayButtonFactory
{
    internal static Button CreateMenuButton(
        string name,
        string text,
        int size,
        Color bgColor,
        Color borderColor,
        Action onPress)
    {
        var button = CreateBase(name, text, size);
        ApplyMenuStyle(button, bgColor, borderColor, FontSizeFor(size), SettingsStore.GetMenuButtonOpacity());
        button.Pressed += onPress;
        return button;
    }

    internal static void ApplySize(Button button, int size)
    {
        if (!NodeQuery.IsLive(button))
            return;

        button.CustomMinimumSize = new Vector2(size, size);
        button.AddThemeFontSizeOverride("font_size", FontSizeFor(size));
        button.ResetSize();
    }

    internal static void ApplyMenuStyle(Button button, Color bgColor, Color borderColor, int fontSize, float opacity)
    {
        ApplyFlatStyle(button, bgColor, borderColor, fontSize, opacity, cornerRadius: 14, borderWidth: 3, margin: 6);
    }

    internal static void ApplyCardStyle(Button button, int fontSize, float opacity)
    {
        ApplyFlatStyle(
            button,
            new Color(0.08f, 0.1f, 0.14f, 0.95f),
            new Color(0.45f, 0.85f, 1f, 1f),
            fontSize,
            opacity,
            cornerRadius: 6,
            borderWidth: 2,
            margin: 2);
    }

    private static Button CreateBase(string name, string text, int size) =>
        new()
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(size, size),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };

    private static void ApplyFlatStyle(
        Button button,
        Color bgColor,
        Color borderColor,
        int fontSize,
        float opacity,
        int cornerRadius,
        int borderWidth,
        int margin)
    {
        var style = new StyleBoxFlat
        {
            BgColor = WithOpacity(bgColor, opacity),
            BorderColor = WithOpacity(borderColor, opacity),
            BorderWidthBottom = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            ContentMarginLeft = margin,
            ContentMarginRight = margin,
            ContentMarginTop = margin,
            ContentMarginBottom = margin
        };

        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("focus", style);
        button.AddThemeFontSizeOverride("font_size", fontSize);
    }

    internal static Color WithOpacity(Color color, float opacity) =>
        new(color.R, color.G, color.B, color.A * opacity);

    private static int FontSizeFor(int size) =>
        Math.Clamp(size / 5, 14, 28);
}
