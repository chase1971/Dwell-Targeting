using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

/// <summary>
/// Disables NMouseCardPlay while dwell targeting is active so cards cannot be dragged.
/// </summary>
internal static class MouseCardPlayBlocker
{
    private static NMouseCardPlay? _mouseCardPlay;
    private static bool _blocked;
    private static bool _savedProcess;
    private static bool _savedInput;
    private static bool _savedUnhandledInput;
    private static Node.ProcessModeEnum _savedMode;

    internal static void Sync(bool shouldBlock)
    {
        if (!shouldBlock)
        {
            Release();
            return;
        }

        var node = FindMouseCardPlay();
        if (node == null)
            return;

        if (!_blocked)
        {
            _savedProcess = node.IsProcessing();
            _savedInput = node.IsProcessingInput();
            _savedUnhandledInput = node.IsProcessingUnhandledInput();
            _savedMode = node.ProcessMode;

            node.SetProcess(false);
            node.SetProcessInput(false);
            node.SetProcessUnhandledInput(false);
            node.ProcessMode = Node.ProcessModeEnum.Disabled;
            _blocked = true;
            ModLogger.Info("NMouseCardPlay disabled.");
        }
    }

    internal static void Release()
    {
        if (!_blocked || _mouseCardPlay == null || !NodeQuery.IsLive(_mouseCardPlay))
        {
            _blocked = false;
            _mouseCardPlay = null;
            return;
        }

        _mouseCardPlay.ProcessMode = _savedMode;
        _mouseCardPlay.SetProcess(_savedProcess);
        _mouseCardPlay.SetProcessInput(_savedInput);
        _mouseCardPlay.SetProcessUnhandledInput(_savedUnhandledInput);
        _blocked = false;
        ModLogger.Info("NMouseCardPlay restored.");
    }

    private static NMouseCardPlay? FindMouseCardPlay()
    {
        if (_mouseCardPlay != null && NodeQuery.IsLive(_mouseCardPlay))
            return _mouseCardPlay;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return null;

        var found = NodeQuery.FindAll<NMouseCardPlay>(tree.Root);
        _mouseCardPlay = found.FirstOrDefault();
        return _mouseCardPlay;
    }
}
