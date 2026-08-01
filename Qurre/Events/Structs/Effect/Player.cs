using HarmonyLib;
using InventorySystem.Items.ThrowableProjectiles;
using JetBrains.Annotations;
using Qurre.API;
using Qurre.API.Controllers;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Qurre.Events.Structs;

[PublicAPI]
public class PlayerFlashedEvent : IBaseEvent
{
    private static readonly AccessTools.FieldRef<FlashbangGrenade, float> MinimalEffectDurationRef =
        AccessTools.FieldRefAccess<FlashbangGrenade, float>("_minimalEffectDuration");
    
    private const uint EventID = EffectEvents.Flashed;

    internal PlayerFlashedEvent(Player player, FlashbangGrenade grenade, float duration)
    {
        Player = player;
        Grenade = grenade;

        Thrower = grenade.PreviousOwner.Hub.GetPlayer() ?? Server.Host;
        Position = grenade.transform.position;

        Allowed = duration > MinimalEffectDurationRef(grenade);
    }

    public Player Player { get; }
    public Player Thrower { get; }
    public FlashbangGrenade Grenade { get; }
    public Vector3 Position { get; }
    public bool Allowed { get; set; }
    public uint EventId { get; } = EventID;
}