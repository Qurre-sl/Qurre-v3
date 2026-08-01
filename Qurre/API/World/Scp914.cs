using System;
using HarmonyLib;
using JetBrains.Annotations;
using Scp914;
using UnityEngine;
using Utils.ConfigHandler;
using Object = UnityEngine.Object;

namespace Qurre.API.World;

[PublicAPI]
public static class Scp914
{
    private static readonly Action<Scp914Controller, Transform> SetIntakeChamber =
        AccessTools.MethodDelegate<Action<Scp914Controller, Transform>>(
            AccessTools.PropertySetter(typeof(Scp914Controller), "IntakeChamber"));

    private static readonly Action<Scp914Controller, Transform> SetOutputChamber =
        AccessTools.MethodDelegate<Action<Scp914Controller, Transform>>(
            AccessTools.PropertySetter(typeof(Scp914Controller), "OutputChamber"));
    
    private static readonly AccessTools.FieldRef<Scp914Controller, Scp914KnobSetting> KnobSettingRef =
        AccessTools.FieldRefAccess<Scp914Controller, Scp914KnobSetting>("_knobSetting");
    
    static Scp914()
    {
        Controller = Object.FindObjectOfType<Scp914Controller>();
    }

    public static Scp914Controller Controller { get; internal set; }

    public static GameObject GameObject => Controller.gameObject;
    public static bool Working => Controller.IsUpgrading;
    public static Vector3 MoveVector => Scp914Controller.MoveVector;

    public static Scp914KnobSetting KnobState
    {
        get => KnobSettingRef(Controller);
        set => Controller.Network_knobSetting = value;
    }

    public static ConfigEntry<Scp914Mode> Config
    {
        get => Controller.ConfigMode;
        set => Controller.ConfigMode = value;
    }

    public static Transform Intake
    {
        get => Controller.IntakeChamber;
        set => SetIntakeChamber(Controller, value);
    }

    public static Transform Output
    {
        get => Controller.OutputChamber;
        set => SetOutputChamber(Controller, value);
    }

    public static void Activate()
    {
        Controller.ServerInteract(Server.Host.ReferenceHub, 0);
    }
}