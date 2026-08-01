using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InventorySystem.Items.Armor;
using JetBrains.Annotations;
using PlayerRoles.FirstPersonControl;
using RemoteAdmin;
using UnityEngine;

namespace Qurre.Internal.Patches.Misc.Fixes;

[HarmonyPatch]
[PublicAPI]
internal static class FixCrush141
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FirstPersonMovementModule), "UpdateMovement")]
    private static bool UpdateMovement(FirstPersonMovementModule __instance)
    {
        return __instance is not null &&
               __instance.Motor != null && __instance.CharControllerSet && __instance.CharController != null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FpcMotor), nameof(FpcMotor.UpdatePosition))]
    private static bool UpdatePosition(FpcMotor __instance)
    {
        return __instance is not null && __instance.MainModule != null && __instance.MainModule.CharControllerSet &&
               __instance.MainModule.CharController != null;
    }
    
    private static readonly AccessTools.FieldRef<QueryProcessor, PlayerCommandSender> SenderRef = 
        AccessTools.FieldRefAccess<QueryProcessor, PlayerCommandSender>("_sender");

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QueryProcessor), "OnDestroy")]
    private static bool BodyArmorUpdate(QueryProcessor __instance)
    {
        var sender = SenderRef(__instance);
        return sender is { OutputId: not null };
    }
    
    private static readonly AccessTools.FieldRef<BodyArmorPickup, Rigidbody> RbRef = 
        AccessTools.FieldRefAccess<BodyArmorPickup, Rigidbody>("_rb");

    private static readonly AccessTools.FieldRef<BodyArmorPickup, float> RemainingReleaseTimeRef = 
        AccessTools.FieldRefAccess<BodyArmorPickup, float>("_remainingReleaseTime");

    private static readonly AccessTools.FieldRef<BodyArmorPickup, bool> ReleasedRef = 
        AccessTools.FieldRefAccess<BodyArmorPickup, bool>("_released");

    private static readonly Func<BodyArmorPickup, bool> IsAffectedGetter = 
        AccessTools.MethodDelegate<Func<BodyArmorPickup, bool>>(
            AccessTools.PropertyGetter(typeof(BodyArmorPickup), "IsAffected"));


    [HarmonyPrefix]
    [HarmonyPatch(typeof(BodyArmorPickup), "Update")]
    private static bool BodyArmorUpdate(BodyArmorPickup __instance)
    {
        try
        {
            if (!IsAffectedGetter(__instance) || Mathf.Abs(RbRef(__instance).linearVelocity.y) > 0.10000000149011612)
                return false;

            RemainingReleaseTimeRef(__instance) -= Time.deltaTime;

            if (RemainingReleaseTimeRef(__instance) > 0.0)
                return false;

            ReleasedRef(__instance) = true;
            RbRef(__instance).constraints = RigidbodyConstraints.None;
        }
        catch
        {
        }

        return false;
    }


    /// <summary>
    ///     Полностью заменяем оригинальный метод. <br />
    ///     • Если <c>gameObject</c> ещё жив – возвращаем его HashCode (оригинальное поведение).<br />
    ///     • Если объект уже уничтожен – используем <c>GetInstanceID()</c> (Unity выдаёт
    ///     уникальный int даже для “псевдо-null” объектов).<br />
    ///     • На крайний случай (теоретически невозможно, но на всякий случай) берём
    ///     «идентичный» хэш через <c>RuntimeHelpers.GetHashCode</c>.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ReferenceHub), nameof(ReferenceHub.GetHashCode))]
    private static bool GetOrAddPatch(ReferenceHub __instance, ref int __result)
    {
        // 1) ReferenceHub сам по себе может оказаться псевдо-null
        if (!__instance)
        {
            __result = 0;
            return false; // пропускаем оригинал
        }

        try
        {
            // 2) Нормальный путь — живой GameObject
            GameObject go = __instance.gameObject;
            if (go) // оператор "bool" у UnityEngine.Object
            {
                __result = go.GetHashCode();
                return false;
            }

            // 3) GameObject уже уничтожен → fallback
            __result = __instance.GetInstanceID();
            return false;
        }
        catch
        {
            // 4) Абсолютный запасной вариант (не должен понадобиться)
            __result = RuntimeHelpers.GetHashCode(__instance);
            return false;
        }
    }
}