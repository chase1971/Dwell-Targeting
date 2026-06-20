using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace DwellTargeting;

internal static class ShopInventoryQuery
{
    internal static bool IsInventoryOpen(Node searchRoot)
    {
        foreach (var inventory in NodeQuery.FindAll<NMerchantInventory>(searchRoot))
        {
            if (!NodeQuery.IsLive(inventory))
                continue;

            if (inventory.IsOpen)
                return true;
        }

        return false;
    }
}
