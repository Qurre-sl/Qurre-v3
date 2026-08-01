using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using InventorySystem.Items.Radio;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.API.Objects;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;

namespace Qurre.Internal.Patches.PlayerEvents.Items;

[HarmonyPatch(typeof(RadioItem), nameof(RadioItem.ServerProcessCmd))]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class UpdateRadio
{
    private static readonly AccessTools.FieldRef<RadioItem, byte> RangeIdRef =
        AccessTools.FieldRefAccess<RadioItem, byte>("_rangeId");

    private static readonly AccessTools.FieldRef<RadioItem, bool> EnabledRef =
        AccessTools.FieldRefAccess<RadioItem, bool>("_enabled");

    private static readonly AccessTools.FieldRef<RadioItem, float> BatteryRef =
        AccessTools.FieldRefAccess<RadioItem, float>("_battery");

    private static readonly MethodInfo SendStatusMessageMethod =
        AccessTools.Method(typeof(RadioItem), "SendStatusMessage");
    
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Call(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0); // RadioItem
        yield return new CodeInstruction(OpCodes.Ldarg_1); // RadioCommand
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UpdateRadio), nameof(Invoke)));
        yield return new CodeInstruction(OpCodes.Ret);
    }

    private static void Invoke(RadioItem instance, RadioMessages.RadioCommand command)
    {
        byte range = RangeIdRef(instance);
        bool enabled = EnabledRef(instance);
        Player? player = instance.Owner.GetPlayer();

        if (player is null)
            return;

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (command)
        {
            case RadioMessages.RadioCommand.Enable:
                if (BatteryRef(instance) > 0f)
                    enabled = true;
                break;

            case RadioMessages.RadioCommand.Disable:
                enabled = false;
                break;

            case RadioMessages.RadioCommand.ChangeRange:
            {
                byte b = (byte)(range + 1);
                if (b >= instance.Ranges.Length)
                    b = 0;

                range = b;
                break;
            }
        }

        UpdateRadioEvent ev = new(player, instance, (RadioStatus)range, enabled);
        ev.InvokeEvent();

        if (!ev.Allowed) return;

        RangeIdRef(instance) = (byte)ev.Range;
        EnabledRef(instance) = ev.Enabled;

        SendStatusMessageMethod.Invoke(instance, null);
    }
}