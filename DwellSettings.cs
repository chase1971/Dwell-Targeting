using System.Text.Json.Serialization;

namespace DwellTargeting;

internal sealed class DwellSettings
{
    // Card number buttons: 1.0 = smallest (default), up to 1.5 = larger.
    public const float CardMinScale = 1f;
    public const float CardMaxScale = 1.5f;

    // E / pile / map buttons: 1.0 = default (largest), down to 0.5 = smallest.
    public const float MenuMinScale = 0.5f;
    public const float MenuMaxScale = 1f;

    public const float MinOpacity = 0.25f;
    public const float MaxOpacity = 1f;

    [JsonPropertyName("hideEndTurnButton")]
    public bool HideEndTurnButton { get; set; }

    [JsonPropertyName("hideOverlaysInMenus")]
    public bool HideOverlaysInMenus { get; set; } = true;

    [JsonPropertyName("hideConfirmButton")]
    public bool HideConfirmButton { get; set; }

    [JsonPropertyName("cardButtonScale")]
    public float CardButtonScale { get; set; } = 1f;

    [JsonPropertyName("actionButtonScale")]
    public float ActionButtonScale { get; set; } = 1f;

    [JsonPropertyName("utilityButtonScale")]
    public float UtilityButtonScale { get; set; } = 1f;

    [JsonPropertyName("cardButtonOpacity")]
    public float CardButtonOpacity { get; set; } = 0.95f;

    [JsonPropertyName("menuButtonOpacity")]
    public float MenuButtonOpacity { get; set; } = 0.92f;

    [JsonPropertyName("showDrawPileButton")]
    public bool ShowDrawPileButton { get; set; } = true;

    [JsonPropertyName("showDiscardPileButton")]
    public bool ShowDiscardPileButton { get; set; } = true;

    [JsonPropertyName("showDeckButton")]
    public bool ShowDeckButton { get; set; } = true;

    [JsonPropertyName("showExhaustPileButton")]
    public bool ShowExhaustPileButton { get; set; } = true;

    [JsonPropertyName("showMapButton")]
    public bool ShowMapButton { get; set; } = true;

    [JsonPropertyName("showMenuButton")]
    public bool ShowMenuButton { get; set; } = true;
}
