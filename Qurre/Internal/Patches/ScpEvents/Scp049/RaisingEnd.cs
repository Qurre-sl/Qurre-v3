using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.Ragdolls;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.API.Helpers;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;

namespace Qurre.Internal.Patches.ScpEvents.Scp049;

[HarmonyPatch(typeof(Scp049ResurrectAbility), "ServerComplete")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class RaisingEnd
{
    private static readonly Func<Scp049ResurrectAbility, BasicRagdoll> CurRagdollGetter =
        ReflectionHelper.PropertyGetter<Scp049ResurrectAbility, BasicRagdoll>("CurRagdoll");
    
    [HarmonyPrefix]
    private static bool Call(Scp049ResurrectAbility __instance)
    {
        BasicRagdoll curRagdoll = CurRagdollGetter(__instance);

        if (curRagdoll == null)
            return false;

        Player? target = curRagdoll.Info.OwnerHub.GetPlayer();
        Player? player = __instance.Owner.GetPlayer();

        if (target is null || player is null)
            return false;

        Scp049RaisingEndEvent @event = new(player, target, curRagdoll);
        @event.InvokeEvent();

        return @event.Allowed;
    }
}