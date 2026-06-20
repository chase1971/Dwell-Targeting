using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace DwellTargeting;

/// <summary>
/// Opens the merchant rug via <see cref="NMerchantButton"/> and purchases inventory entries by invoking the
/// slot's own <c>OnTryPurchase(MerchantInventory)</c> handler directly. That path is tied to the specific slot
/// object, so it is position-independent — unlike synthetic clicks, which the game routes through whatever slot
/// it considers physically hovered (causing the offset number button to wiggle the neighbouring card).
/// </summary>
internal static class ShopSelectionService
{
    private static bool _dumpedSlotMembers;

    internal static void TryPurchase(Control control, int slot)
    {
        if (!NodeQuery.IsLive(control))
        {
            ModLogger.Warn($"Shop target #{slot} not live.");
            return;
        }

        if (control is NMerchantButton)
        {
            TryOpenMerchant(control, slot);
            return;
        }

        var merchantSlot = control as NMerchantSlot ?? FindParentMerchantSlot(control);
        if (merchantSlot != null)
        {
            TryPurchaseMerchantSlot(merchantSlot, slot);
            return;
        }

        if (InputForwardService.TryActivateControl(control))
            ModLogger.Info($"Shop target #{slot} '{control.Name}' activated.");
        else
            ModLogger.Warn($"Shop target #{slot} '{control.Name}' activation failed.");
    }

    internal static bool SlotContains<T>(NMerchantSlot slot) where T : Node
    {
        foreach (var node in NodeQuery.FindAll<T>(slot))
        {
            if (NodeQuery.IsLive(node))
                return true;
        }

        return false;
    }

    private static void TryOpenMerchant(Control control, int slot)
    {
        if (control is NClickableControl clickable)
        {
            clickable.ForceClick();
            ModLogger.Info($"Shop merchant #{slot} '{control.Name}' opened via ForceClick.");
            return;
        }

        if (InputForwardService.TryActivateControl(control))
            ModLogger.Info($"Shop merchant #{slot} '{control.Name}' opened.");
        else
            ModLogger.Warn($"Shop merchant #{slot} '{control.Name}' open failed.");
    }

    private static void TryPurchaseMerchantSlot(NMerchantSlot slot, int slotNum)
    {
        DumpSlotMembersOnce(slot);

        var type = slot.GetType();

        // Primary: slot's own OnTryPurchase(MerchantInventory) — position-independent, scoped to this slot.
        var inventory = ResolveInventoryModel(slot);
        if (inventory != null && TryInvokeOnTryPurchase(type, slot, inventory, slotNum))
            return;

        // Fallback: drive the slot's mouse handlers with hover forced on.
        ForceHoverState(slot, type);
        if (TryInvokeSlotMethod(type, slot, "OnMousePressed", slotNum)
            && TryInvokeSlotMethod(type, slot, "OnMouseReleased", slotNum))
        {
            ModLogger.Info($"Shop slot #{slotNum} '{slot.Name}' purchase via OnMousePressed/Released (hover forced).");
            return;
        }

        ModLogger.Warn($"Shop slot #{slotNum} '{slot.Name}' purchase failed — no usable path (inventory={(inventory != null)}).");
    }

    private static bool TryInvokeOnTryPurchase(Type type, NMerchantSlot slot, MerchantInventory inventory, int slotNum)
    {
        try
        {
            var method = FindOnTryPurchaseMethod(type);
            if (method == null)
                return false;

            method.Invoke(slot, new object[] { inventory });
            ModLogger.Info($"Shop slot #{slotNum} '{slot.Name}' purchase via OnTryPurchase.");
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Shop slot #{slotNum} OnTryPurchase failed: {ex.Message}");
            return false;
        }
    }

    private static MethodInfo? FindOnTryPurchaseMethod(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            foreach (var method in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.Name != "OnTryPurchase")
                    continue;

                var ps = method.GetParameters();
                if (ps.Length == 1 && typeof(MerchantInventory).IsAssignableFrom(ps[0].ParameterType))
                    return method;
            }
        }

        return null;
    }

    private static MerchantInventory? ResolveInventoryModel(NMerchantSlot slot)
    {
        // The slot holds the inventory NODE (_merchantRug); the model lives as a field/prop on that node.
        var rug = ReadMember(slot, "_merchantRug") as Node;
        if (rug == null)
            return null;

        if (FindValueOfType<MerchantInventory>(rug) is { } model)
            return model;

        return null;
    }

    private static T? FindValueOfType<T>(object target) where T : class
    {
        var type = target.GetType();
        for (var t = type; t != null && t != typeof(Node); t = t.BaseType)
        {
            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (typeof(T).IsAssignableFrom(field.FieldType) && field.GetValue(target) is T value)
                    return value;
            }

            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!typeof(T).IsAssignableFrom(prop.PropertyType) || prop.GetIndexParameters().Length != 0)
                    continue;

                try
                {
                    if (prop.GetValue(target) is T value)
                        return value;
                }
                catch
                {
                    // Some getters throw when the node isn't fully initialised; ignore.
                }
            }
        }

        return null;
    }

    private static object? ReadMember(object target, string name)
    {
        var field = FindField(target.GetType(), name);
        return field?.GetValue(target);
    }

    private static void ForceHoverState(NMerchantSlot slot, Type type)
    {
        SetBoolField(type, slot, "_isHovered", true);
        SetBoolField(type, slot, "_ignoreMouseRelease", false);
    }

    private static void SetBoolField(Type type, object target, string fieldName, bool value)
    {
        try
        {
            var field = FindField(type, fieldName);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(target, value);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Shop slot set {fieldName}={value} failed: {ex.Message}");
        }
    }

    private static bool TryInvokeSlotMethod(Type type, NMerchantSlot slot, string methodName, int slotNum)
    {
        try
        {
            var method = FindMethod(type, methodName);
            if (method == null)
            {
                ModLogger.Warn($"Shop slot #{slotNum} method '{methodName}' not found.");
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                method.Invoke(slot, null);
            else if (parameters.Length == 1 && typeof(InputEvent).IsAssignableFrom(parameters[0].ParameterType))
                method.Invoke(slot, new object[] { CreateMouseButton(slot) });
            else
                method.Invoke(slot, new object?[parameters.Length]);

            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Shop slot #{slotNum} invoke '{methodName}' failed: {ex.Message}");
            return false;
        }
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field;
        }

        return null;
    }

    private static MethodInfo? FindMethod(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var method = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
                return method;
        }

        return null;
    }

    private static InputEventMouseButton CreateMouseButton(Control control)
    {
        var globalPos = control.GetGlobalRect().GetCenter();
        var localPos = control.GetGlobalTransformWithCanvas().AffineInverse() * globalPos;
        return new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            GlobalPosition = globalPos,
            Position = localPos
        };
    }

    private static void DumpSlotMembersOnce(NMerchantSlot slot)
    {
        if (_dumpedSlotMembers)
            return;

        _dumpedSlotMembers = true;

        try
        {
            var type = slot.GetType();
            ModLogger.Info($"[ShopDump] slot type={type.FullName} base={type.BaseType?.FullName}");

            for (var t = type; t != null && t != typeof(Node); t = t.BaseType)
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    ModLogger.Info($"[ShopDump] {t.Name} field {field.Name} : {field.FieldType.Name}");

                foreach (var method in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var ps = method.GetParameters();
                    var sig = string.Join(",", Array.ConvertAll(ps, p => p.ParameterType.Name));
                    ModLogger.Info($"[ShopDump] {t.Name} method {method.Name}({sig})");
                }
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"[ShopDump] failed: {ex.Message}");
        }
    }

    private static NMerchantSlot? FindParentMerchantSlot(Control control)
    {
        var parent = control.GetParent();
        while (parent != null)
        {
            if (parent is NMerchantSlot slot)
                return slot;

            parent = parent.GetParent();
        }

        return null;
    }
}
