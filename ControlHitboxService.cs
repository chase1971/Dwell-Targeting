using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rewards;

namespace DwellTargeting;

/// <summary>
/// Builds generous global hitboxes from a control and its visible descendants.
/// </summary>
internal static class ControlHitboxService
{
    private const float MinWidth = 48f;
    private const float MinHeight = 32f;
    private const float Padding = 6f;

    internal static bool TryGetDwellRect(Control root, out Rect2 rect, float extraPadding = 0f)
    {
        rect = default;
        if (!NodeQuery.IsLive(root))
            return false;

        float pad = Padding + extraPadding;
        bool hasBounds = false;
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        IncludeControl(root, ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);

        if (root is NRewardButton || root is NProceedButton)
            IncludeDescendants(root, ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);

        if (!hasBounds)
            return false;

        rect = new Rect2(
            minX - pad,
            minY - pad,
            (maxX - minX) + (pad * 2f),
            (maxY - minY) + (pad * 2f));

        if (rect.Size.X < MinWidth)
        {
            float expand = (MinWidth - rect.Size.X) * 0.5f;
            rect = new Rect2(rect.Position.X - expand, rect.Position.Y, MinWidth, Math.Max(rect.Size.Y, MinHeight));
        }

        if (rect.Size.Y < MinHeight)
        {
            float expand = (MinHeight - rect.Size.Y) * 0.5f;
            rect = new Rect2(rect.Position.X, rect.Position.Y - expand, rect.Size.X, MinHeight);
        }

        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    private static void IncludeDescendants(Node node, ref bool hasBounds, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        if (!NodeQuery.IsLive(node))
            return;

        try
        {
            foreach (var child in node.GetChildren())
            {
                if (child is Control control)
                    IncludeControl(control, ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);

                IncludeDescendants(child, ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);
            }
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    private static void IncludeControl(Control control, ref bool hasBounds, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        if (!NodeQuery.IsVisible(control))
            return;

        var candidate = control.GetGlobalRect();
        if (candidate.Size.X < 4f || candidate.Size.Y < 4f)
            return;

        hasBounds = true;
        minX = Math.Min(minX, candidate.Position.X);
        minY = Math.Min(minY, candidate.Position.Y);
        maxX = Math.Max(maxX, candidate.End.X);
        maxY = Math.Max(maxY, candidate.End.Y);
    }
}
