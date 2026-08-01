using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using MapGeneration.Distributors;
using Mirror;
using Qurre.API.Addons;
using Qurre.API.Controllers.Components;
using Qurre.API.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Qurre.API.Controllers;

[PublicAPI]
public class Generator : NetTransform
{
    private static readonly MethodInfo HasFlagMethod =
        AccessTools.Method(typeof(Scp079Generator), "HasFlag",
            new[] { typeof(Scp079Generator.GeneratorFlags), typeof(Scp079Generator.GeneratorFlags) });

    private static readonly AccessTools.FieldRef<Scp079Generator, byte> FlagsRef =
        AccessTools.FieldRefAccess<Scp079Generator, byte>("_flags");

    private static readonly MethodInfo ServerSetFlagMethod =
        AccessTools.Method(typeof(Scp079Generator), "ServerSetFlag",
            new[] { typeof(Scp079Generator.GeneratorFlags), typeof(bool) });

    private static readonly AccessTools.FieldRef<Scp079Generator, float> TargetCooldownRef =
        AccessTools.FieldRefAccess<Scp079Generator, float>("_targetCooldown");

    private static readonly AccessTools.FieldRef<Scp079Generator, float> DoorToggleCooldownTimeRef =
        AccessTools.FieldRefAccess<Scp079Generator, float>("_doorToggleCooldownTime");

    private static readonly AccessTools.FieldRef<Scp079Generator, float> UnlockCooldownTimeRef =
        AccessTools.FieldRefAccess<Scp079Generator, float>("_unlockCooldownTime");

    private static readonly AccessTools.FieldRef<Scp079Generator, Stopwatch> LeverStopwatchRef =
        AccessTools.FieldRefAccess<Scp079Generator, Stopwatch>("_leverStopwatch");

// _syncTime — private get, но обычно у [SyncVar] бэкинг-поле генерируется как приватное с публичным Network-сеттером
    private static readonly AccessTools.FieldRef<Scp079Generator, short> SyncTimeRef =
        AccessTools.FieldRefAccess<Scp079Generator, short>("_syncTime");
    
    private static bool HasFlag(Scp079Generator gen, Scp079Generator.GeneratorFlags flags, Scp079Generator.GeneratorFlags flag)
        => (bool)HasFlagMethod.Invoke(gen, new object[] { flags, flag });

    private static void ServerSetFlag(Scp079Generator gen, Scp079Generator.GeneratorFlags flag, bool value)
        => ServerSetFlagMethod.Invoke(gen, new object[] { flag, value });
    
    private readonly Scp079Generator _generator;
    private readonly StructurePositionSync _positionSync;
    private string _name = string.Empty;

    internal Generator(Scp079Generator g)
    {
        _generator = g;
        _positionSync = _generator.GetComponent<StructurePositionSync>();
        SetupActions();
    }

    public Generator(Vector3 position, Quaternion? rotation = null)
    {
        if (Prefabs.Generator == null)
            throw new NullReferenceException(nameof(Prefabs.Generator));

        _generator = Object.Instantiate(Prefabs.Generator);

        _generator.transform.position = position;
        _generator.transform.rotation = rotation ?? new Quaternion();

        _positionSync = _generator.GetComponent<StructurePositionSync>();

        SetupActions();
        NetworkServer.Spawn(_generator.gameObject);

        _generator.netIdentity.UpdateData();

        Map.Generators.Add(this);
    }

    public override GameObject GameObject => _generator.gameObject;

    public string Name
    {
        get => string.IsNullOrEmpty(_name) ? GameObject.name : _name;
        set => _name = value;
    }

    public bool Open
    {
        get => HasFlag(_generator, (Scp079Generator.GeneratorFlags)FlagsRef(_generator), Scp079Generator.GeneratorFlags.Open);
        set
        {
            ServerSetFlag(_generator, Scp079Generator.GeneratorFlags.Open, value);
            TargetCooldownRef(_generator) = DoorToggleCooldownTimeRef(_generator);
        }
    }

    public bool Lock
    {
        get => !HasFlag(_generator, (Scp079Generator.GeneratorFlags)FlagsRef(_generator), Scp079Generator.GeneratorFlags.Unlocked);
        set
        {
            ServerSetFlag(_generator, Scp079Generator.GeneratorFlags.Unlocked, !value);
            TargetCooldownRef(_generator) = UnlockCooldownTimeRef(_generator);
        }
    }

    public bool Active
    {
        get => _generator.Activating;
        set
        {
            _generator.Activating = value;
            if (value) LeverStopwatchRef(_generator).Restart();
            TargetCooldownRef(_generator) = DoorToggleCooldownTimeRef(_generator);
        }
    }

    public bool Engaged
    {
        get => _generator.Engaged;
        set => _generator.Engaged = value;
    }

    public short Time
    {
        get => SyncTimeRef(_generator);
        set => _generator.Network_syncTime = value;
    }

    private void SetupActions()
    {
        OnPositionUpdate += () => _positionSync.Network_position = Position;
        OnRotationUpdate += () => _positionSync.Network_rotationY = (sbyte)(Rotation.eulerAngles.y / 5.625f);
    }

    public override void Destroy()
    {
        NetworkServer.Destroy(GameObject);
        Map.Generators.Remove(this);
    }
}