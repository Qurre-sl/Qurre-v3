using Footprinting;
using HarmonyLib;
using JetBrains.Annotations;
using Mirror;
using Qurre.API.Controllers.Components;
using Qurre.API.World;
using UnityEngine;

namespace Qurre.API.Controllers;

[PublicAPI]
public class Window : NetTransform
{
    private static readonly AccessTools.FieldRef<BreakableWindow, bool> PreventScpDamageRef =
        AccessTools.FieldRefAccess<BreakableWindow, bool>("_preventScpDamage");
    
    private string _name;

    internal Window(BreakableWindow window)
    {
        _name = "Window";
        Breakable = window;
    }

    public BreakableWindow Breakable { get; }
    public bool AllowBreak { get; set; } = true;

    public override GameObject GameObject => Breakable.gameObject;
    public Footprint LastAttacker => Breakable.LastAttacker;

    public string Name
    {
        get
        {
            if (string.IsNullOrEmpty(_name))
                _name = "Window";

            return _name;
        }
        set => _name = value;
    }

    public bool PreventScpDamage
    {
        get => PreventScpDamageRef(Breakable);
        set => PreventScpDamageRef(Breakable) = value;
    }

    public float Hp
    {
        get => Breakable.Health;
        set => Breakable.Health = value;
    }

    public bool IsBroken
    {
        get => Breakable.NetworkIsBroken;
        set => Breakable.NetworkIsBroken = value;
    }

    public override void Destroy()
    {
        NetworkServer.Destroy(GameObject);
        Map.Windows.Remove(this);
    }
}