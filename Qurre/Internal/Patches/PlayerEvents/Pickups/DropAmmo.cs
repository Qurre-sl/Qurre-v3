using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using InventorySystem;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.API.Helpers;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;

namespace Qurre.Internal.Patches.PlayerEvents.Pickups;

[HarmonyPatch(typeof(InventoryExtensions), nameof(InventoryExtensions.ServerDropAmmo))]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class DropAmmo
{
    private static readonly AccessTools.FieldRef<Inventory, ReferenceHub> HubRef =
        ReflectionHelper.FieldRef<Inventory, ReferenceHub>("_hub");
    
    [HarmonyPrefix]
    private static bool Call(Inventory inv, ref ItemType ammoType, ref ushort amount)
    {
        try
        {
            Player? pl = HubRef(inv).GetPlayer();

            if (pl is null)
                return false;

            DropAmmoEvent ev = new(pl, ammoType.GetAmmoType(), amount);
            ev.InvokeEvent();

            ammoType = ev.Type.GetItemType();
            amount = ev.Amount;

            return ev.Allowed;
        }
        catch (Exception e)
        {
            Log.Error($"Patch Error - <Player> {{Pickups}} [DropAmmo]: {e}\n{e.StackTrace}");
            return true;
        }
    }
}