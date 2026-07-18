using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Owns the per-card button rows shown over the hand during combat play and hand-select.
/// Extracted from <see cref="HandTargetingOverlay"/> so that class stays a thin per-frame
/// coordinator. Holds the live row map and the "hand block" rect that swallows stray clicks
/// on the cards themselves.
/// </summary>
internal static class CombatRowsCoordinator
{
    private const int GapAboveCard = CardButtonRow.DefaultGapAboveCard;
    private const float HandBlockPadding = 24f;

    private static readonly Dictionary<ulong, CardButtonRow> _rows = new();
    private static Rect2 _handBlockBounds;

    /// <summary>Build / refresh the play-mode button rows (1..N target buttons per card).</summary>
    internal static void SyncCombatPlay(NPlayerHand hand, Control? fallbackRoot)
    {
        long enemyStart = OverlayPerfDiagnostics.BeginTick();
        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = runState == null ? null : MegaCrit.Sts2.Core.Context.LocalContext.GetMe(runState);
        var enemies = EnemyOrderService.GetAliveEnemiesLeftToRight(player?.Creature.CombatState);
        int enemyCount = enemies.Count;
        OverlayPerfDiagnostics.Add("combat.enemyFetch", enemyStart);

        long holderStart = OverlayPerfDiagnostics.BeginTick();
        var holders = GetPlayModeHolders(hand);
        OverlayPerfDiagnostics.Add("combat.holderFetch", holderStart);

        HandLayoutDiagnostics.MaybeLog(hand, holders);

        int handSize = holders.Count(h => h.CardModel != null && NodeQuery.IsVisible(h));
        int buttonSize = SettingsStore.GetCardButtonSize(handSize);

        var visibleHolders = holders
            .Where(h => h.CardModel != null && NodeQuery.IsVisible(h))
            .ToList();
        var slotIndexFromRight = new Dictionary<ulong, int>(visibleHolders.Count);
        for (int i = 0; i < visibleHolders.Count; i++)
            slotIndexFromRight[visibleHolders[i].GetInstanceId()] = visibleHolders.Count - 1 - i;

        long boundsStart = OverlayPerfDiagnostics.BeginTick();
        Rect2 handBounds = ComputeHandBounds(holders);
        OverlayPerfDiagnostics.Add("combat.handBounds", boundsStart);

        long rowsStart = OverlayPerfDiagnostics.BeginTick();
        var liveIds = new HashSet<ulong>();
        foreach (var holder in holders)
        {
            var card = holder.CardModel;
            if (card == null || !NodeQuery.IsVisible(holder))
                continue;
            if (!card.CanPlay(out _, out _))
                continue;

            ulong id = holder.GetInstanceId();
            liveIds.Add(id);

            if (!_rows.TryGetValue(id, out var row))
            {
                row = new CardButtonRow(holder, fallbackRoot);
                _rows[id] = row;
                ModLogger.Info($"Button row for {card.Id.Entry} holder={id} parented={(holder is Control)}");
            }

            int fromRight = slotIndexFromRight.TryGetValue(id, out int idxFromRight) ? idxFromRight : int.MaxValue;
            int gapAboveCard = CardButtonRow.ResolveGapAboveCard(handSize, fromRight);
            row.SyncPlay(card, enemyCount, holder, buttonSize, gapAboveCard);
        }

        _handBlockBounds = handBounds;
        RemoveStaleRows(liveIds);
        OverlayPerfDiagnostics.Add("combat.rowsSync", rowsStart);

        long labelStart = OverlayPerfDiagnostics.BeginTick();
        if (SettingsStore.Current.ShowEnemyLabels)
            EnemyLabelOverlay.Sync(enemies, handSize);
        else
            EnemyLabelOverlay.Hide();
        OverlayPerfDiagnostics.Add("combat.labels", labelStart);
    }

    /// <summary>Add every live row's dwell targets to the frame's target list.</summary>
    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        foreach (var row in _rows.Values)
            row.CollectDwellTargets(targets);
    }

    /// <summary>Route a click to the first row that contains it.</summary>
    internal static bool TryActivateAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var row in _rows.Values)
        {
            if (row.TryActivateAt(globalPos, out message))
                return true;
        }

        return false;
    }

    /// <summary>True if any row's button sits under the point (used to spare it from hand-blocking).</summary>
    internal static bool TryHitAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var row in _rows.Values)
        {
            if (row.TryHitAt(globalPos, out message))
                return true;
        }

        return false;
    }

    /// <summary>True if the point falls inside the hand-block rect (a stray click on the cards).</summary>
    internal static bool HandBlockContains(Vector2 globalPos)
    {
        if (_handBlockBounds.Size.X < 1 || _handBlockBounds.Size.Y < 1)
            return false;

        return _handBlockBounds.HasPoint(globalPos);
    }

    /// <summary>Dispose every row and clear the hand-block rect.</summary>
    internal static void Clear()
    {
        foreach (var row in _rows.Values)
            row.Dispose();
        _rows.Clear();
        _handBlockBounds = new Rect2(0, 0, 0, 0);
    }

    private static void RemoveStaleRows(HashSet<ulong> liveIds)
    {
        foreach (var pair in _rows.ToList())
        {
            if (!liveIds.Contains(pair.Key))
            {
                pair.Value.Dispose();
                _rows.Remove(pair.Key);
            }
        }
    }

    private static Rect2 ComputeHandBounds(IReadOnlyList<NCardHolder> holders)
    {
        if (holders.Count == 0)
            return new Rect2(0, 0, 0, 0);

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var holder in holders)
        {
            if (!NodeQuery.IsVisible(holder))
                continue;

            if (!CardAnchorService.TryGetCardRect(holder, out Rect2 cardRect))
                continue;

            minX = Math.Min(minX, cardRect.Position.X);
            maxX = Math.Max(maxX, cardRect.End.X);
            minY = Math.Min(minY, cardRect.Position.Y);
            maxY = Math.Max(maxY, cardRect.End.Y);
        }

        if (minX == float.MaxValue)
            return new Rect2(0, 0, 0, 0);

        float topPad = GapAboveCard + 90f;
        return new Rect2(
            minX - HandBlockPadding,
            minY - topPad,
            (maxX - minX) + (HandBlockPadding * 2f),
            (maxY - minY) + topPad + HandBlockPadding);
    }

    private static List<NCardHolder> GetPlayModeHolders(NPlayerHand hand)
    {
        var holders = new List<NCardHolder>();
        foreach (var holderObj in hand.ActiveHolders)
        {
            if (holderObj is NCardHolder typed)
                holders.Add(typed);
        }

        if (holders.Count == 0)
            holders.AddRange(NodeQuery.FindAllSortedByPosition<NCardHolder>(hand));

        holders.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
            return cmp != 0 ? cmp : a.GetInstanceId().CompareTo(b.GetInstanceId());
        });

        return holders;
    }
}
