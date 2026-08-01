using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using InventorySystem.Items.Jailbird;
using Mirror;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;

namespace Qurre.Internal.Patches.PlayerEvents.Items;

[HarmonyPatch(typeof(JailbirdItem), nameof(JailbirdItem.ServerProcessCmd))]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class JailbirdTrigger
{
    private static readonly AccessTools.FieldRef<JailbirdItem, bool> ChargingRef =
        AccessTools.FieldRefAccess<JailbirdItem, bool>("_charging");

    private static readonly AccessTools.FieldRef<JailbirdItem, double> ChargeResetTimeRef =
        AccessTools.FieldRefAccess<JailbirdItem, double>("_chargeResetTime");

    private static readonly AccessTools.FieldRef<JailbirdItem, bool> FirstChargeFrameRef =
        AccessTools.FieldRefAccess<JailbirdItem, bool>("_firstChargeFrame");

    private static readonly AccessTools.FieldRef<JailbirdItem, Stopwatch> ChargeLoadStopwatchRef =
        AccessTools.FieldRefAccess<JailbirdItem, Stopwatch>("_chargeLoadStopwatch");

    private static readonly AccessTools.FieldRef<JailbirdItem, bool> ChargeAnyDetectedRef =
        AccessTools.FieldRefAccess<JailbirdItem, bool>("_chargeAnyDetected");

// SendRpc(JailbirdMessageType) — 1 параметр
    private static readonly MethodInfo SendRpcMethod1 =
        AccessTools.Method(typeof(JailbirdItem), "SendRpc", new[] { typeof(JailbirdMessageType) });

// SendRpc(JailbirdMessageType, Action<NetworkWriter>) — 2 параметра
    private static readonly MethodInfo SendRpcMethod2 =
        AccessTools.Method(typeof(JailbirdItem), "SendRpc", new[] { typeof(JailbirdMessageType), typeof(Action<NetworkWriter>) });
    
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Call(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> list = [.. instructions];

        int index = list.FindLastIndex(ins => ins.opcode == OpCodes.Stloc_0) + 1;

        if (index < 1)
        {
            Log.Error($"Creating Patch error: <Player> {{Items}} [JailbirdTrigger]: Index - {index} < 1");
            return list.AsEnumerable();
        }

        list.InsertRange(index,
        [
            new CodeInstruction(OpCodes.Ldarg_0), // @base [JailbirdItem]
            new CodeInstruction(OpCodes.Ldloc_0), // message [JailbirdMessageType]
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JailbirdTrigger), nameof(Invoke))),
            new CodeInstruction(OpCodes.Stloc_0)
        ]);

        return list.AsEnumerable();
    }
    
    private static JailbirdMessageType Invoke(JailbirdItem @base, JailbirdMessageType message)
    {
        if (message is JailbirdMessageType.ChargeStarted && ChargingRef(@base) &&
            NetworkTime.time - ChargeResetTimeRef(@base) < .5)
            return JailbirdMessageType.UpdateState;

        Player? player = @base.Owner.GetPlayer();

        if (player is null)
            return message;

        JailbirdTriggerEvent @event = new(player, @base, message);
        @event.InvokeEvent();

        if (@event.Allowed)
            return @event.Message;

        @event.Message = JailbirdMessageType.UpdateState;

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (message)
        {
            case JailbirdMessageType.ChargeStarted:
                ChargingRef(@base) = true;
                FirstChargeFrameRef(@base) = true;
                ChargeLoadStopwatchRef(@base).Reset();
                ChargeAnyDetectedRef(@base) = false;
                ChargeResetTimeRef(@base) = NetworkTime.time;

                SendRpcMethod2.Invoke(@base, new object[]
                {
                    JailbirdMessageType.ChargeStarted,
                    (Action<NetworkWriter>)delegate(NetworkWriter wr) { wr.WriteDouble(ChargeResetTimeRef(@base)); }
                });

                SendRpcMethod1.Invoke(@base, new object[] { JailbirdMessageType.ChargeFailed });
                break;

            case JailbirdMessageType.ChargeFailed:
                @event.Message = JailbirdMessageType.ChargeFailed;
                break;
        }

        return @event.Message;
    }
}