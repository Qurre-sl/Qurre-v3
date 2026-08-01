using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using PlayerRoles.PlayableScps.Scp106;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;

namespace Qurre.Internal.Patches.ScpEvents.Scp106;

[HarmonyPatch(typeof(Scp106Attack), "ServerShoot")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class Attack
{
    private static readonly AccessTools.FieldRef<Scp106Attack, ReferenceHub> TargetHubRef =
        AccessTools.FieldRefAccess<Scp106Attack, ReferenceHub>("_targetHub");
    
    [HarmonyPrefix]
    private static bool Call(Scp106Attack __instance)
    {
        Player? attacker = __instance.Owner.GetPlayer();
        Player? target = TargetHubRef(__instance).GetPlayer();

        if (attacker is null || target is null)
            return false;

        Scp106AttackEvent ev = new(attacker, target);
        ev.InvokeEvent();

        return ev.Allowed;
    }
}