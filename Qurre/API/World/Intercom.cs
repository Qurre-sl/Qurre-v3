using System;
using HarmonyLib;
using JetBrains.Annotations;
using Mirror;
using PlayerRoles.Voice;
using Qurre.API.Controllers;
using BaseIntercom = PlayerRoles.Voice.Intercom;

namespace Qurre.API.World;

[PublicAPI]
public static class Intercom
{
    private static readonly AccessTools.FieldRef<IntercomDisplay> DisplaySingletonRef =
        AccessTools.StaticFieldRefAccess<IntercomDisplay>(AccessTools.Field(typeof(IntercomDisplay), "_singleton"));

    private static readonly AccessTools.FieldRef<BaseIntercom> BaseSingletonRef =
        AccessTools.StaticFieldRefAccess<BaseIntercom>(AccessTools.Field(typeof(BaseIntercom), "_singleton"));

    private static readonly AccessTools.FieldRef<BaseIntercom, ReferenceHub> CurSpeakerRef =
        AccessTools.FieldRefAccess<BaseIntercom, ReferenceHub>("_curSpeaker");

    private static readonly AccessTools.FieldRef<IntercomDisplay, string> OverrideTextRef =
        AccessTools.FieldRefAccess<IntercomDisplay, string>("_overrideText");

    private static readonly AccessTools.FieldRef<BaseIntercom, double> NextTimeRef =
        AccessTools.FieldRefAccess<BaseIntercom, double>("_nextTime");

    private static readonly AccessTools.FieldRef<BaseIntercom, float> CooldownTimeRef =
        AccessTools.FieldRefAccess<BaseIntercom, float>("_cooldownTime");

    public static IntercomDisplay Display => DisplaySingletonRef();
    public static BaseIntercom Base => BaseSingletonRef();

    public static Player? Speaker => CurSpeakerRef(Base).GetPlayer();

    public static string Text
    {
        get => OverrideTextRef(Display);
        set => Display.Network_overrideText = value;
    }

    public static IntercomState Status
    {
        get => BaseIntercom.State;
        set => BaseIntercom.State = value;
    }

    public static double RemainingCooldown
    {
        get => Status == IntercomState.Cooldown ? Math.Max(NextTimeRef(Base) - NetworkTime.time, 0) : 0;
        set => NextTimeRef(Base) = value + NetworkTime.time;
    }

    public static float RechargeCooldown
    {
        get => CooldownTimeRef(Base);
        set => CooldownTimeRef(Base) = value;
    }

    public static float SpeechRemaining
    {
        get => Base.RemainingTime;
        set => NextTimeRef(Base) = NetworkTime.time + value;
    }
}