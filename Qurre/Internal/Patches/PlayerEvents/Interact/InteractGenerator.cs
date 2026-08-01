using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using Footprinting;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using MapGeneration.Distributors;
using PlayerRoles;
using Qurre.API;
using Qurre.API.Controllers;
using Qurre.API.Objects;
using Qurre.Events.Structs;
using Qurre.Internal.EventsManager;

namespace Qurre.Internal.Patches.PlayerEvents.Interact;

[HarmonyPatch(typeof(Scp079Generator), nameof(Scp079Generator.ServerInteract))]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class InteractGenerator
{
    private static readonly AccessTools.FieldRef<Scp079Generator, Stopwatch> CooldownStopwatchRef =
        AccessTools.FieldRefAccess<Scp079Generator, Stopwatch>("_cooldownStopwatch");

    private static readonly AccessTools.FieldRef<Scp079Generator, float> TargetCooldownRef =
        AccessTools.FieldRefAccess<Scp079Generator, float>("_targetCooldown");

    private static readonly AccessTools.FieldRef<Scp079Generator, byte> FlagsRef =
        AccessTools.FieldRefAccess<Scp079Generator, byte>("_flags");

    private static readonly MethodInfo HasFlagMethod =
        AccessTools.Method(typeof(Scp079Generator), "HasFlag",
            new[] { typeof(Scp079Generator.GeneratorFlags), typeof(Scp079Generator.GeneratorFlags) });

    private static readonly MethodInfo ServerSetFlagMethod =
        AccessTools.Method(typeof(Scp079Generator), "ServerSetFlag",
            new[] { typeof(Scp079Generator.GeneratorFlags), typeof(bool) });

    private static readonly AccessTools.FieldRef<Scp079Generator, float> DoorToggleCooldownTimeRef =
        AccessTools.FieldRefAccess<Scp079Generator, float>("_doorToggleCooldownTime");

    private static readonly AccessTools.FieldRef<Scp079Generator, float> UnlockCooldownTimeRef =
        AccessTools.FieldRefAccess<Scp079Generator, float>("_unlockCooldownTime");

    private static readonly AccessTools.FieldRef<Scp079Generator, Footprint> LastActivatorRef =
        AccessTools.FieldRefAccess<Scp079Generator, Footprint>("_lastActivator");

    private static readonly AccessTools.FieldRef<Scp079Generator, Stopwatch> LeverStopwatchRef =
        AccessTools.FieldRefAccess<Scp079Generator, Stopwatch>("_leverStopwatch");

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Call(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0); // Scp079Generator [instance]
        yield return new CodeInstruction(OpCodes.Ldarg_1); // ReferenceHub [ply]
        yield return new CodeInstruction(OpCodes.Ldarg_2); // byte [colliderId]
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InteractGenerator), nameof(Invoke)));
        yield return new CodeInstruction(OpCodes.Ret);
    }

    private static void Invoke(Scp079Generator instance, ReferenceHub ply, byte colliderId)
    {
        try
        {
            if (CooldownStopwatchRef(instance).IsRunning &&
                CooldownStopwatchRef(instance).Elapsed.TotalSeconds < TargetCooldownRef(instance))
                return;

            bool HasFlag(Scp079Generator.GeneratorFlags flags, Scp079Generator.GeneratorFlags flag) =>
                (bool)HasFlagMethod.Invoke(instance, new object[] { flags, flag });

            void ServerSetFlag(Scp079Generator.GeneratorFlags flag, bool value) =>
                ServerSetFlagMethod.Invoke(instance, new object[] { flag, value });

            if (colliderId != 0 && !HasFlag((Scp079Generator.GeneratorFlags)FlagsRef(instance),
                    Scp079Generator.GeneratorFlags.Open))
                return;

            CooldownStopwatchRef(instance).Stop();

            Player? pl = ply.GetPlayer();
            if (pl is null)
                return;

            switch (colliderId)
            {
                case 0: // Open, Close, Unlock
                {
                    if (HasFlag((Scp079Generator.GeneratorFlags)FlagsRef(instance),
                            Scp079Generator.GeneratorFlags.Unlocked))
                    {
                        bool opened = HasFlag((Scp079Generator.GeneratorFlags)FlagsRef(instance),
                            Scp079Generator.GeneratorFlags.Open);

                        InteractGeneratorEvent ev = new(pl, instance.GetGenerator(),
                            opened ? GeneratorStatus.CloseDoor : GeneratorStatus.OpenDoor);
                        ev.InvokeEvent();

                        if (ev.Allowed)
                            ServerSetFlag(Scp079Generator.GeneratorFlags.Open, !opened);
                        else
                            instance.RpcDenied(ply.GetCombinedPermissions(instance));

                        TargetCooldownRef(instance) = DoorToggleCooldownTimeRef(instance);
                    }
                    else
                    {
                        InteractGeneratorEvent ev = new(pl, instance.GetGenerator(), GeneratorStatus.Unlock);

                        ev.Allowed =
                            instance.PermissionsPolicy.CheckPermissions(ply, instance, out PermissionUsed callback);

                        ev.InvokeEvent();

                        if (ev.Allowed)
                        {
                            ServerSetFlag(Scp079Generator.GeneratorFlags.Unlocked, true);
                            callback?.Invoke(instance, true);
                        }
                        else
                        {
                            TargetCooldownRef(instance) = UnlockCooldownTimeRef(instance);
                            instance.RpcDenied(ply.GetCombinedPermissions(instance));
                            callback?.Invoke(instance, false);
                        }
                    }

                    break;
                }
                case 1: // Activate / Disable
                    if ((ply.IsHuman() || instance.Activating) && !instance.Engaged)
                    {
                        InteractGeneratorEvent ev = new(pl, instance.GetGenerator(),
                            instance.Activating ? GeneratorStatus.Deactivate : GeneratorStatus.Activate);
                        ev.InvokeEvent();

                        if (!ev.Allowed)
                            break;

                        instance.Activating = !instance.Activating;

                        if (!instance.Activating)
                        {
                            LastActivatorRef(instance) = default;
                        }
                        else
                        {
                            LeverStopwatchRef(instance).Restart();
                            LastActivatorRef(instance) = new Footprint(ply);
                        }

                        TargetCooldownRef(instance) = DoorToggleCooldownTimeRef(instance);
                    }

                    break;
                case 2:
                    if (instance is { Activating: true, Engaged: false })
                    {
                        InteractGeneratorEvent ev = new(pl, instance.GetGenerator(), GeneratorStatus.Deactivate);
                        ev.InvokeEvent();

                        if (!ev.Allowed)
                            break;

                        ServerSetFlag(Scp079Generator.GeneratorFlags.Activating, false);
                        TargetCooldownRef(instance) = UnlockCooldownTimeRef(instance);
                        LastActivatorRef(instance) = default;
                    }

                    break;
                default:
                    TargetCooldownRef(instance) = 1f;
                    break;
            }

            CooldownStopwatchRef(instance).Restart();
        }
        catch (Exception e)
        {
            Log.Error($"Patch Error - <Player> {{Interact}} [Generator]: {e}\n{e.StackTrace}");
        }
    }
}