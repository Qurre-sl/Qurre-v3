using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.Ragdolls;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;
using UnityEngine;

namespace Qurre.Internal.Patches.ScpEvents.Scp049;

using static Scp049ResurrectAbility;

[HarmonyPatch(typeof(Scp049ResurrectAbility), "ServerValidateAny")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class RaisingStart
{
    // ---------- Кэшированные аксессоры ----------

    // protected BasicRagdoll CurRagdoll { get; private set; }
    private static readonly Func<Scp049ResurrectAbility, BasicRagdoll> CurRagdollGetter =
        AccessTools.MethodDelegate<Func<Scp049ResurrectAbility, BasicRagdoll>>(
            AccessTools.PropertyGetter(typeof(Scp049ResurrectAbility), "CurRagdoll"));

    // private bool IsCloseEnough(Vector3, Vector3)
    private static readonly MethodInfo IsCloseEnoughMethod =
        AccessTools.Method(typeof(Scp049ResurrectAbility), "IsCloseEnough");

    // private Transform _ragdollTransform;
    private static readonly AccessTools.FieldRef<Scp049ResurrectAbility, Transform> RagdollTransformRef =
        AccessTools.FieldRefAccess<Scp049ResurrectAbility, Transform>("_ragdollTransform");

    // private static bool IsSpawnableSpectator(ReferenceHub)
    private static readonly MethodInfo IsSpawnableSpectatorMethod =
        AccessTools.Method(typeof(Scp049ResurrectAbility), "IsSpawnableSpectator");

    // private ResurrectError CheckMaxResurrections(ReferenceHub)
    private static readonly MethodInfo CheckMaxResurrectionsMethod =
        AccessTools.Method(typeof(Scp049ResurrectAbility), "CheckMaxResurrections");

    // private enum ResurrectError { None, ... }
    private static readonly Type ResurrectErrorType =
        AccessTools.Inner(typeof(Scp049ResurrectAbility), "ResurrectError");

    private static readonly object ResurrectErrorNone =
        Enum.Parse(ResurrectErrorType, "None");

    // private bool AnyConflicts(Ragdoll)
    private static readonly MethodInfo AnyConflictsMethod =
        AccessTools.Method(typeof(Scp049ResurrectAbility), "AnyConflicts");

    // ---------- Транспайлер (без изменений) ----------

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Call(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0); // instance [Scp049ResurrectAbility]
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RaisingStart), nameof(Invoke)));
        yield return new CodeInstruction(OpCodes.Ret);
    }

    // ---------- Основная логика ----------

    private static bool Invoke(Scp049ResurrectAbility instance)
    {
        BasicRagdoll curRagdoll = CurRagdollGetter(instance);

        if (curRagdoll == null)
            return false;

        Player? target = curRagdoll.Info.OwnerHub.GetPlayer();
        Player? player = instance.Owner.GetPlayer();

        if (target is null || player is null)
            return false;

        Scp049RaisingStartEvent @event = new(player, target, curRagdoll);

        bool isCloseEnough = (bool)IsCloseEnoughMethod.Invoke(instance,
            new object[] { instance.CastRole.FpcModule.Position, RagdollTransformRef(instance).position });

        bool isSpawnableSpectator = (bool)IsSpawnableSpectatorMethod.Invoke(null,
            new object[] { target.ReferenceHub });

        object resurrectResult = CheckMaxResurrectionsMethod.Invoke(instance,
            new object[] { target.ReferenceHub });
        bool checkPassed = ResurrectErrorNone.Equals(resurrectResult);

        bool anyConflicts = (bool)AnyConflictsMethod.Invoke(instance,
            new object[] { @event.Corpse.Base });

        @event.Allowed =
            isCloseEnough
            && isSpawnableSpectator
            && checkPassed
            && !anyConflicts;

        @event.InvokeEvent();

        return @event.Allowed;
    }
}