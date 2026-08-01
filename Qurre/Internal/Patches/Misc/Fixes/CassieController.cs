using System;
using System.Diagnostics.CodeAnalysis;
using Cassie;
using HarmonyLib;
using LabApi.Features.Wrappers;
using MEC;
using Qurre.API;
using Map = Qurre.API.World.Map;

namespace Qurre.Internal.Patches.Misc.Fixes;

[HarmonyPatch(typeof(CassieAnnouncementDispatcher), nameof(CassieAnnouncementDispatcher.AddToQueue))]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class CassieController
{
    [HarmonyPrefix]
    private static bool Call(CassieAnnouncement __instance)
    {
        try
        {
            string words = __instance.Payload.Content;
            bool makeHold = __instance.Payload.PlayBackground;
            bool makeNoise = __instance.GlitchScale > 0f;

            foreach (API.Controllers.Cassie cassie in Map.Cassies)
            {
                if (cassie.Message == words && cassie.Hold == makeHold && cassie.Noise == makeNoise)
                {
                    Map.Cassies.Remove(cassie);
                    Timing.CallDelayed(
                        (float)Announcer.CalculateDuration(words, new CassiePlaybackModifiers()), 
                        API.Controllers.Cassie.End
                    );
                    return false;
                }
            }

            Map.Cassies.Add(new API.Controllers.Cassie(words, makeHold, makeNoise), true);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Patch Error - <Misc> {{Fixes}} [CassieController]: {e}\n{e.StackTrace}");
            return true;
        }
    }
}