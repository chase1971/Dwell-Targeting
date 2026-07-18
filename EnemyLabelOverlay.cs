using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

/// <summary>
/// Display-only slot numbers above enemies so they match card target buttons.
/// </summary>
internal static class EnemyLabelOverlay
{
    private const int CanvasLayerOrder = 126;
    private const int GapAboveHitbox = 32;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly Dictionary<ulong, EnemyBadge> _badges = new();

    internal static void Sync(IReadOnlyList<Creature> enemies, int handSize)
    {
        if (!SettingsStore.Current.ShowEnemyLabels
            || !SettingsStore.AreOverlayVisualsVisible()
            || enemies.Count == 0)
        {
            Hide();
            return;
        }

        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;
        var nodes = EnemyOrderService.GetVisibleEnemyNodesCached();
        int badgeSize = Math.Clamp(SettingsStore.GetCardButtonSize(handSize) - 2, 30, 44);
        float opacity = SettingsStore.GetCardButtonOpacity();
        var liveIds = new HashSet<ulong>();

        for (int slot = 0; slot < enemies.Count; slot++)
        {
            var creature = enemies[slot];
            var node = EnemyOrderService.FindNodeForCreature(creature, nodes);
            if (node == null)
                continue;

            ulong id = node.GetInstanceId();
            liveIds.Add(id);

            if (!_badges.TryGetValue(id, out var badge))
            {
                badge = new EnemyBadge(_root);
                _badges[id] = badge;
            }

            badge.Sync(slot + 1, node, badgeSize, opacity);
        }

        foreach (var pair in _badges.ToList())
        {
            if (!liveIds.Contains(pair.Key))
            {
                pair.Value.Dispose();
                _badges.Remove(pair.Key);
            }
        }
    }

    internal static void Hide()
    {
        // Idempotent: when labels are disabled this is called every frame. Doing the teardown work
        // (and invalidating the shared enemy-node cache) each frame forced a full scene-tree scan
        // every frame, so bail out cheaply once we're already hidden.
        bool alreadyHidden = _badges.Count == 0 && (_root == null || !_root.Visible);
        if (alreadyHidden)
            return;

        foreach (var badge in _badges.Values)
            badge.Dispose();
        _badges.Clear();
        EnemyOrderService.InvalidateNodeCache();

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellEnemyLabelsLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellEnemyLabelsRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);
    }

    private sealed class EnemyBadge
    {
        private readonly Control _host;
        private readonly Button _badge;
        private NCreature? _creature;
        private int _slot;
        private int _size;
        private float _opacity = -1f;

        internal EnemyBadge(Control root)
        {
            _host = new Control
            {
                Name = "EnemyBadgeHost",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 50
            };
            root.AddChild(_host);

            _badge = new Button
            {
                Name = "EnemyBadge",
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _host.AddChild(_badge);
        }

        internal void Sync(int slot, NCreature creature, int size, float opacity)
        {
            _creature = creature;
            bool slotChanged = slot != _slot;
            bool sizeChanged = size != _size;
            bool opacityChanged = Math.Abs(opacity - _opacity) > 0.001f;

            _slot = slot;
            _size = size;
            _opacity = opacity;

            if (slotChanged)
                _badge.Text = slot.ToString();

            if (sizeChanged)
                OverlayButtonFactory.ApplySize(_badge, size);

            if (sizeChanged || opacityChanged)
                RefreshAppearance();

            Reposition();
            _host.Visible = true;
            _badge.Visible = true;
        }

        internal void Reposition()
        {
            if (_creature == null || !NodeQuery.IsLive(_creature) || !NodeQuery.IsVisible(_creature))
            {
                _host.Visible = false;
                return;
            }

            if (!EnemyOrderService.TryGetLabelAnchor(_creature, out var centerTop))
            {
                _host.Visible = false;
                return;
            }

            float x = centerTop.X - (_size * 0.5f);
            float y = centerTop.Y - GapAboveHitbox - _size;
            _badge.GlobalPosition = new Vector2(x, y);
            _badge.Size = new Vector2(_size, _size);
            _host.Visible = true;
        }

        internal void Dispose()
        {
            if (NodeQuery.IsLive(_host))
                _host.QueueFree();
        }

        private void RefreshAppearance()
        {
            int fontSize = Math.Max(12, _size / 2);
            OverlayButtonFactory.ApplyCardStyle(_badge, fontSize, _opacity);
        }
    }
}
