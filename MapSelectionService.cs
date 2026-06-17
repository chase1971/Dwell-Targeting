using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DwellTargeting;

/// <summary>
/// Activates a travelable map node (same proven ForceClick path used for reward buttons).
/// </summary>
internal static class MapSelectionService
{
    internal static void TrySelect(NMapPoint point)
    {
        if (!NodeQuery.IsLive(point))
        {
            ModLogger.Warn("MapSelect: node not live.");
            return;
        }

        if (point.State != MapPointState.Travelable)
        {
            ModLogger.Info($"MapSelect: node not travelable (state={point.State}) — ignoring.");
            return;
        }

        point.ForceClick();
        ModLogger.Info("MapSelect: ForceClick on travelable map node.");
    }
}
