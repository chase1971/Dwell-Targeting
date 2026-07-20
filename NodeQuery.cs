using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

internal static class NodeQuery
{
    internal static bool IsLive(Node? node)
    {
        try
        {
            return node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsVisible(CanvasItem item)
    {
        try
        {
            return IsLive(item) && item.Visible && item.IsVisibleInTree();
        }
        catch
        {
            return false;
        }
    }

    internal static List<T> FindAll<T>(Node start) where T : Node
    {
        var found = new List<T>();
        if (!IsLive(start))
            return found;

        int visited = 0;
        FindAllRecursive(start, found, ref visited, pruneInvisible: false);
        NoteTreeWalk(visited);
        return found;
    }

    /// <summary>Like FindAll but skips recursing into hidden CanvasItem subtrees.</summary>
    internal static List<T> FindAllVisible<T>(Node start) where T : Node
    {
        var found = new List<T>();
        if (!IsLive(start))
            return found;

        int visited = 0;
        FindAllVisibleRecursive(start, found, ref visited);
        NoteTreeWalk(visited);
        return found;
    }

    internal static List<T> FindAllSortedByPosition<T>(Node start) where T : Control
    {
        var list = FindAll<T>(start);
        list.Sort((left, right) =>
        {
            int cmp = left.GlobalPosition.Y.CompareTo(right.GlobalPosition.Y);
            return cmp != 0 ? cmp : left.GlobalPosition.X.CompareTo(right.GlobalPosition.X);
        });
        return list;
    }

    internal static void SetMouseFilterIgnoreRecursive(Node node, List<(Control Control, Control.MouseFilterEnum Original)>? restoreList = null)
    {
        if (!IsLive(node))
            return;

        if (node is Control control)
        {
            restoreList?.Add((control, control.MouseFilter));
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        try
        {
            foreach (var child in node.GetChildren())
                SetMouseFilterIgnoreRecursive(child, restoreList);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    internal static void RestoreMouseFilters(IEnumerable<(Control Control, Control.MouseFilterEnum Original)> entries)
    {
        foreach (var (control, original) in entries)
        {
            if (!IsLive(control))
                continue;

            try
            {
                control.MouseFilter = original;
            }
            catch
            {
                /* disposed */
            }
        }
    }

    private static void NoteTreeWalk(int visited)
    {
        OverlayPerfDiagnostics.Count("tree.findAllCalls");
        OverlayPerfDiagnostics.Count("tree.nodesVisited", visited);
        ModHealthReporter.NoteTreeWalk(visited);
    }

    private static void FindAllRecursive<T>(Node node, List<T> found, ref int visited, bool pruneInvisible) where T : Node
    {
        if (!IsLive(node))
            return;

        visited++;

        if (node is T match)
            found.Add(match);

        if (pruneInvisible && node is CanvasItem canvas && !IsVisible(canvas))
            return;

        RecurseChildren(node, found, ref visited, pruneInvisible);
    }

    private static void FindAllVisibleRecursive<T>(Node node, List<T> found, ref int visited) where T : Node
    {
        FindAllRecursive(node, found, ref visited, pruneInvisible: true);
    }

    private static void RecurseChildren<T>(Node node, List<T> found, ref int visited, bool pruneInvisible) where T : Node
    {
        try
        {
            foreach (var child in node.GetChildren())
                FindAllRecursive(child, found, ref visited, pruneInvisible);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }
}
