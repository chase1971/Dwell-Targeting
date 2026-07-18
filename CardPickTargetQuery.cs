using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DwellTargeting;

/// <summary>
/// Finds card-pick dwell targets (holders, card visuals, skip) on reward draft / pile-select screens.
/// </summary>
internal static class CardPickTargetQuery
{
    private const float MinCardWidth = 40f;
    private const float MinCardHeight = 60f;

    internal static List<NCardHolder> FindHolders(Node root)
    {
        var list = new List<NCardHolder>();
        foreach (var holder in NodeQuery.FindAll<NCardHolder>(root))
        {
            if (!IsSelectableHolder(holder))
                continue;

            list.Add(holder);
        }

        list.Sort((a, b) =>
        {
            if (a is not Control ca || b is not Control cb)
                return 0;

            int cmp = ca.GlobalPosition.X.CompareTo(cb.GlobalPosition.X);
            return cmp != 0 ? cmp : ca.GlobalPosition.Y.CompareTo(cb.GlobalPosition.Y);
        });

        return list;
    }

    internal static List<Control> FindCardControls(Node root)
    {
        var list = new List<Control>();
        var seen = new HashSet<ulong>();

        foreach (var card in NodeQuery.FindAll<NCard>(root))
        {
            if (card is not Control control || !NodeQuery.IsVisible(control))
                continue;

            var rect = control.GetGlobalRect();
            if (rect.Size.X < MinCardWidth || rect.Size.Y < MinCardHeight)
                continue;

            // Ignore tiny card icons in the top bar / piles.
            if (rect.Position.Y < 120f)
                continue;

            ulong id = control.GetInstanceId();
            if (!seen.Add(id))
                continue;

            list.Add(control);
        }

        list.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
            return cmp != 0 ? cmp : a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
        });

        return list;
    }

    internal static List<Control> FindSkipControls(Node root)
    {
        var list = new List<Control>();

        foreach (var button in NodeQuery.FindAll<NProceedButton>(root))
        {
            if (button is Control control && IsLiveButton(control))
                list.Add(control);
        }

        foreach (var button in NodeQuery.FindAll<NChoiceSelectionSkipButton>(root))
        {
            if (button is Control control && IsLiveButton(control) && !list.Contains(control))
                list.Add(control);
        }

        AddControlsByTypeName(root, list, "NCardRewardAlternativeButton");
        AddSkipByLabel(root, list);

        return list;
    }

    internal static bool TryGetDwellRect(Control control, out Rect2 rect, float extraPadding = 0f)
    {
        if (ControlHitboxService.TryGetDwellRect(control, out rect, extraPadding))
            return true;

        if (!NodeQuery.IsLive(control) || !NodeQuery.IsVisible(control))
            return false;

        rect = control.GetGlobalRect();
        if (extraPadding > 0f)
            rect = rect.Grow(extraPadding);

        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    internal readonly struct CachedPickTarget
    {
        internal Rect2 Bounds { get; init; }
        internal Action Activate { get; init; }
        internal string Name { get; init; }
        internal bool Menu { get; init; }
    }

    /// <summary>Measure rects once when the screen is scanned — Collect reuses these, no per-frame layout reads.</summary>
    internal static List<CachedPickTarget> BuildCachedPickTargets(
        IReadOnlyList<NCardHolder>? holders,
        IReadOnlyList<Control>? cardControls,
        IReadOnlyList<Control>? skipControls,
        float skipPadding = 12f,
        Action<Control>? skipActivate = null)
    {
        var list = new List<CachedPickTarget>();

        if (holders is { Count: > 0 })
        {
            int slot = 1;
            foreach (var holder in holders)
            {
                if (!NodeQuery.IsLive(holder)
                    || !CardAnchorService.TryGetCardRect(holder, out var cardRect)
                    || cardRect.Size.X < 8f
                    || cardRect.Size.Y < 8f)
                {
                    slot++;
                    continue;
                }

                var captured = holder;
                int capturedSlot = slot;
                list.Add(new CachedPickTarget
                {
                    Bounds = cardRect,
                    Activate = () => PileCardSelectionService.TrySelect(captured, capturedSlot),
                    Name = $"PickCard:{slot}",
                    Menu = false
                });
                slot++;
            }

            AppendSkipTargets(list, skipControls, skipPadding, skipActivate);
            return list;
        }

        if (cardControls is { Count: > 0 })
        {
            int slot = 1;
            foreach (var card in cardControls)
            {
                if (!NodeQuery.IsLive(card))
                {
                    slot++;
                    continue;
                }

                var rect = card.GetGlobalRect();
                if (rect.Size.X < 8f || rect.Size.Y < 8f)
                {
                    slot++;
                    continue;
                }

                var captured = card;
                int capturedSlot = slot;
                list.Add(new CachedPickTarget
                {
                    Bounds = rect,
                    Activate = () => PileCardSelectionService.TrySelectCardControl(captured, capturedSlot),
                    Name = $"PickCard:{slot}",
                    Menu = false
                });
                slot++;
            }
        }

        AppendSkipTargets(list, skipControls, skipPadding, skipActivate);
        return list;
    }

    internal static void AppendCachedPickTargets(
        IReadOnlyList<CachedPickTarget> cached,
        List<DwellHoverService.Target> targets)
    {
        foreach (var pick in cached)
        {
            if (pick.Menu)
                targets.Add(DwellHoverService.Menu(pick.Bounds, pick.Activate, pick.Name));
            else
                targets.Add(DwellHoverService.Card(pick.Bounds, pick.Activate, pick.Name));
        }
    }

    private static void AppendSkipTargets(
        List<CachedPickTarget> list,
        IReadOnlyList<Control>? skipControls,
        float skipPadding,
        Action<Control>? skipActivate = null)
    {
        if (skipControls == null)
            return;

        foreach (var button in skipControls)
        {
            if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
                continue;

            if (!TryGetDwellRect(button, out var rect, skipPadding))
                continue;

            var captured = button;
            list.Add(new CachedPickTarget
            {
                Bounds = rect,
                Activate = () =>
                {
                    if (skipActivate != null)
                        skipActivate(captured);
                    else
                        InputForwardService.TryActivateControl(captured);
                },
                Name = $"PickSkip:{button.Name}",
                Menu = true
            });
        }
    }

    private static bool IsSelectableHolder(NCardHolder holder)
    {
        if (!NodeQuery.IsLive(holder) || !NodeQuery.IsVisible(holder))
            return false;
        if (holder.CardModel == null)
            return false;

        return CardAnchorService.TryGetCardRect(holder, out var rect)
            && rect.Size.X >= MinCardWidth
            && rect.Size.Y >= MinCardHeight;
    }

    private static bool IsLiveButton(Control control) =>
        NodeQuery.IsLive(control)
        && NodeQuery.IsVisible(control)
        && control is not NClickableControl { IsEnabled: false };

    private static void AddControlsByTypeName(Node node, List<Control> list, string typeName)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (node is Control control
            && node.GetType().Name == typeName
            && IsLiveButton(control)
            && !list.Contains(control))
        {
            list.Add(control);
        }

        try
        {
            foreach (var child in node.GetChildren())
                AddControlsByTypeName(child, list, typeName);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    private static void AddSkipByLabel(Node node, List<Control> list)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (node is Button button
            && NodeQuery.IsVisible(button)
            && IsLiveButton(button)
            && !list.Contains(button))
        {
            string text = button.Text?.Trim() ?? string.Empty;
            if (text.Equals("Skip", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Proceed", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(button);
            }
        }

        try
        {
            foreach (var child in node.GetChildren())
                AddSkipByLabel(child, list);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }
}
