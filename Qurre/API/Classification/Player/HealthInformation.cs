using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using PlayerStatsSystem;
using UnityEngine;

namespace Qurre.API.Classification.Player;

[PublicAPI]
public sealed class HealthInformation
{
    private static readonly MethodInfo KillPlayerMethod =
        AccessTools.Method(typeof(PlayerStats), "KillPlayer", new[] { typeof(DamageHandlerBase) });
    
    private static readonly AccessTools.FieldRef<AhpStat, List<AhpProcess>> ActiveProcessesRef =
        AccessTools.FieldRefAccess<AhpStat, List<AhpProcess>>("_activeProcesses");
    
    private static readonly AccessTools.FieldRef<AhpStat, float> MaxSoFarRef =
        AccessTools.FieldRefAccess<AhpStat, float>("_maxSoFar");
    
    private readonly Controllers.Player _player;
    internal float LocalMaxHp = -1;

    internal HealthInformation(Controllers.Player pl)
    {
        _player = pl;
    }

    public PlayerStats PlayerStats
        => _player.ReferenceHub.playerStats;

    public HealthStat HealthStat
        => (HealthStat)PlayerStats.StatModules[0];

    public AhpStat AhpStat
        => (AhpStat)PlayerStats.StatModules[1];

    public StaminaStat StaminaStat
        => (StaminaStat)PlayerStats.StatModules[2];

    public float Hp
    {
        get => HealthStat.CurValue;
        set => HealthStat.CurValue = value;
    }

    public float MaxHp
    {
        get => HealthStat.MaxValue;
        set => LocalMaxHp = value;
    }

    public List<AhpProcess> AhpActiveProcesses
        => ActiveProcessesRef(AhpStat);
    
    public float Ahp
    {
        get => AhpStat.CurValue;
        set
        {
            if (value > MaxAhp)
                MaxAhp = Mathf.CeilToInt(value);

            AhpProcess? process = AhpActiveProcesses.FirstOrDefault();

            if (process is null)
            {
                AddAhp(value, MaxAhp);
            }
            else
            {
                process.Limit = MaxAhp;
                process.CurrentAmount = value;
            }
        }
    }

    public float MaxAhp
    {
        get => AhpStat.MaxValue;
        set => MaxSoFarRef(AhpStat) = value;
    }

    public float Stamina
    {
        get => StaminaStat.CurValue * 100;
        set => StaminaStat.CurValue = Mathf.Clamp01(value / 100);
    }

    public void Heal(float amount, bool instant)
    {
        if (instant)
            Hp += amount;
        else
            HealthStat.ServerHeal(amount);
    }

    public void AddAhp(float amount, float limit, float decay = 0, float efficacy = 0.7f, float sustain = 0,
        bool persistant = false)
    {
        AhpStat.ServerAddProcess(amount, limit, decay, efficacy, sustain, persistant);
    }

    public void AddStamina(float value)
    {
        StaminaStat.CurValue += value / 100;
    }

    public void Kill(DeathTranslation deathReason)
    {
        KillPlayerMethod.Invoke(PlayerStats, new object[] { new UniversalDamageHandler(-1, deathReason) });
    }

    public void Kill(string deathReason = "")
    {
        KillPlayerMethod.Invoke(PlayerStats, new object[] { new CustomReasonDamageHandler(deathReason) });
    }

    public bool DealDamage(DamageHandlerBase handler)
    {
        return PlayerStats.DealDamage(handler);
    }

    public bool Damage(float damage, string deathReason)
    {
        return DealDamage(new CustomReasonDamageHandler(deathReason, damage));
    }

    public bool Damage(float damage, DeathTranslation deathReason)
    {
        return DealDamage(new UniversalDamageHandler(damage, deathReason));
    }

    public bool Damage(float damage, DeathTranslation deathReason, Controllers.Player attacker)
    {
        return DealDamage(new ScpDamageHandler(attacker.ReferenceHub, damage, deathReason));
    }
}