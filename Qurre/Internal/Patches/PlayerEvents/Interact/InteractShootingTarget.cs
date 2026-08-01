using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.Events;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;

namespace Qurre.Internal.Patches.PlayerEvents.Interact;

[HarmonyPatch(typeof(AdminToys.ShootingTarget), nameof(AdminToys.ShootingTarget.ServerInteract))]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class InteractShootingTarget
{
    private static readonly Type TargetButtonType =
        AccessTools.Inner(typeof(AdminToys.ShootingTarget), "TargetButton");

    private static readonly AccessTools.FieldRef<AdminToys.ShootingTarget, bool> SyncModeRef =
        AccessTools.FieldRefAccess<AdminToys.ShootingTarget, bool>("_syncMode");

    private static readonly MethodInfo UseButtonMethod =
        AccessTools.Method(typeof(AdminToys.ShootingTarget), "UseButton", new[] { TargetButtonType });

    private static readonly AccessTools.FieldRef<AdminToys.ShootingTarget, int> MaxHpRef =
        AccessTools.FieldRefAccess<AdminToys.ShootingTarget, int>("_maxHp");

    private static readonly AccessTools.FieldRef<AdminToys.ShootingTarget, int> AutoDestroyTimeRef =
        AccessTools.FieldRefAccess<AdminToys.ShootingTarget, int>("_autoDestroyTime");

    private static readonly MethodInfo RpcSendInfoMethod =
        AccessTools.Method(typeof(AdminToys.ShootingTarget), "RpcSendInfo",
            new[] { typeof(int), typeof(int) });

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Call(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0); // instance [ShootingTarget]
        yield return new CodeInstruction(OpCodes.Ldarg_1); // ply [ReferenceHub]
        yield return new CodeInstruction(OpCodes.Ldarg_2); // byte [colliderId]
        yield return new CodeInstruction(OpCodes.Call,
            AccessTools.Method(typeof(InteractShootingTarget), nameof(Invoke)));
        yield return new CodeInstruction(OpCodes.Ret);
    }

    private static void Invoke(AdminToys.ShootingTarget instance, ReferenceHub ply, byte colliderId)
    {
        if (!PermissionsHandler.IsPermitted(ply.serverRoles.Permissions, PlayerPermissions.FacilityManagement))
            return;

        Player? player = ply.GetPlayer();
        if (player is null)
            return;

        InteractShootingTargetEvent @event = new(player, instance.GetShootingTarget(), (ShootingTargetButtonType)colliderId);

        switch (colliderId)
        {
            case 5:
                NetworkServer.Destroy(instance.gameObject);
                return;
            case 6:
                instance.Network_syncMode = !SyncModeRef(instance);
                return;
        }

        if (!SyncModeRef(instance) || ply.isLocalPlayer)
            return;

        @event.InvokeEvent();

        if (!@event.Allowed)
            return;

        object internalButtonValue = Enum.ToObject(TargetButtonType, (byte)@event.Button);
        UseButtonMethod.Invoke(instance, new object[] { internalButtonValue });

        RpcSendInfoMethod.Invoke(instance, new object[]
        {
            MaxHpRef(instance), AutoDestroyTimeRef(instance)
        });
    }
}