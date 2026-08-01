using System;
using System.Reflection;
using GameCore;
using HarmonyLib;
using JetBrains.Annotations;
using RoundRestarting;
using UnityEngine;

namespace Qurre.API.World;

[PublicAPI]
public static class Round
{
    private static readonly Action<int> SetEscapedClassD =
        AccessTools.MethodDelegate<Action<int>>(AccessTools.PropertySetter(typeof(RoundSummary), "EscapedClassD"));

    private static readonly Action<int> SetEscapedScientists =
        AccessTools.MethodDelegate<Action<int>>(AccessTools.PropertySetter(typeof(RoundSummary), "EscapedScientists"));

    private static readonly Action<int> SetKilledBySCPs =
        AccessTools.MethodDelegate<Action<int>>(AccessTools.PropertySetter(typeof(RoundSummary), "KilledBySCPs"));

    private static readonly Action<int> SetKills =
        AccessTools.MethodDelegate<Action<int>>(AccessTools.PropertySetter(typeof(RoundSummary), "Kills"));

    private static readonly Action<int> SetChangedIntoZombies =
        AccessTools.MethodDelegate<Action<int>>(AccessTools.PropertySetter(typeof(RoundSummary), "ChangedIntoZombies"));
    
    private static readonly MethodInfo RpcDimScreenMethod =
        AccessTools.Method(typeof(RoundSummary), "RpcDimScreen");

    private static readonly MethodInfo RpcShowRoundSummaryMethod =
        AccessTools.Method(typeof(RoundSummary), "RpcShowRoundSummary");
    
    
    internal static bool ForceEnd;
    internal static bool LocalStarted;
    internal static bool LocalWaiting;

    public static TimeSpan ElapsedTime
        => RoundStart.RoundLength;

    public static DateTime StartedTime
        => DateTime.Now - ElapsedTime;

    public static int CurrentRound { get; internal set; }
    public static int ActiveGenerators { get; internal set; }

    public static short WaitTime
    {
        get => RoundStart.singleton.NetworkTimer;
        set => RoundStart.singleton.NetworkTimer = value;
    }

    public static bool Started
    {
        get
        {
            try
            {
                return LabApi.Features.Wrappers.Round.IsRoundStarted;
            }
            catch
            {
                return LocalStarted;
            }
        }
    }

    public static bool Ended
        => LabApi.Features.Wrappers.Round.IsRoundEnded;

    public static bool Waiting
    {
        get
        {
            try
            {
                if (RoundStart.singleton is null)
                    throw new NullReferenceException("RoundStart.singleton is null");

                return !Started && !Ended;
            }
            catch
            {
                return LocalWaiting;
            }
        }
    }

    public static bool Lock
    {
        get => RoundSummary.RoundLock;
        set => RoundSummary.RoundLock = value;
    }

    public static bool LobbyLock
    {
        get => RoundStart.LobbyLock;
        set => RoundStart.LobbyLock = value;
    }

    public static int EscapedClassD
    {
        get => RoundSummary.EscapedClassD;
        set => SetEscapedClassD(value);
    }

    public static int EscapedScientists
    {
        get => RoundSummary.EscapedScientists;
        set => SetEscapedScientists(value);
    }

    public static int ScpKills
    {
        get => RoundSummary.KilledBySCPs;
        set => SetKilledBySCPs(value);
    }

    public static int RoundKills
    {
        get => RoundSummary.Kills;
        set => SetKills(value);
    }

    public static int ChangedZombies
    {
        get => RoundSummary.ChangedIntoZombies;
        set => SetChangedIntoZombies(value);
    }

    public static void Restart(bool fast = true,
        ServerStatic.NextRoundAction action = ServerStatic.NextRoundAction.DoNothing)
    {
        ServerStatic.StopNextRound = action;
        bool oldFast = CustomNetworkManager.EnableFastRestart;
        CustomNetworkManager.EnableFastRestart = fast;
        RoundRestart.InitiateRoundRestart();
        CustomNetworkManager.EnableFastRestart = oldFast;
    }

    public static void Start()
    {
        CharacterClassManager.ForceRoundStart();
    }

    public static void End()
    {
        ForceEnd = true;
    }

    public static void DimScreen()
    {
        RpcDimScreenMethod.Invoke(RoundSummary.singleton, null);
    }

    public static void ShowRoundSummary(RoundSummary.SumInfo_ClassList remainingPlayers, RoundSummary.LeadingTeam team)
    {
        RpcShowRoundSummaryMethod.Invoke(RoundSummary.singleton, new object[]
        {
            RoundSummary.singleton.classlistStart,          // listStart
            remainingPlayers,                                 // listFinish
            team,                                              // leadingTeam
            EscapedClassD,                                     // eDS
            EscapedScientists,                                 // eSc
            ScpKills,                                          // scpKills
            Mathf.Clamp(ConfigFile.ServerConfig.GetInt("auto_round_restart_time", 10), 5, 1000), // roundCd
            (int)ElapsedTime.TotalSeconds                     // seconds
        });
    }
}