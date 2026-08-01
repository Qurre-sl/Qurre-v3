using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Mirror;
using PlayerRoles;
using Qurre.API.Controllers.Components;
using Qurre.API.World;
using UnityEngine;

namespace Qurre.API.Controllers;

[PublicAPI]
public class Tesla : NetTransform
{
    private static readonly MethodInfo RpcPlayAnimationMethod =
        AccessTools.Method(typeof(TeslaGate), "RpcPlayAnimation");
    
    private string _name;

    internal Tesla(TeslaGate gate)
    {
        _name = string.Empty;
        Gate = gate;
        OnScaleUpdate += () => SizeOfKiller = Scale;
    }

    public TeslaGate Gate { get; }

    public bool Enable { get; set; } = true;
    public bool Allow079Interact { get; set; } = true;

    public List<RoleTypeId> ImmunityRoles { get; } = [];
    public List<Player> ImmunityPlayers { get; } = [];

    public override GameObject GameObject => Gate.gameObject;

    public Vector3 SizeOfKiller
    {
        get => Gate.sizeOfKiller;
        set => Gate.sizeOfKiller = value;
    }

    public string Name
    {
        get => string.IsNullOrEmpty(_name) ? GameObject.name : _name;
        set => _name = value;
    }

    public bool InProgress
    {
        get => Gate.InProgress;
        set => Gate.InProgress = value;
    }

    public float SizeOfTrigger
    {
        get => Gate.sizeOfTrigger;
        set => Gate.sizeOfTrigger = value;
    }
    
    public void Trigger(bool instant = false)
    {
        if (instant) Gate.RpcInstantBurst();
        else RpcPlayAnimationMethod.Invoke(Gate, null);
    }

    public override void Destroy()
    {
        NetworkServer.Destroy(GameObject);
        Map.Teslas.Remove(this);
    }
}