using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using LightContainmentZoneDecontamination;

namespace Qurre.API.World;

[PublicAPI]
public static class Decontamination
{
    private static readonly AccessTools.FieldRef<DecontaminationController, bool> DecontaminationBegunRef =
        AccessTools.FieldRefAccess<DecontaminationController, bool>("_decontaminationBegun");

    private static readonly AccessTools.FieldRef<DecontaminationController, DecontaminationController.DecontaminationStatus> DecontaminationOverrideRef =
        AccessTools.FieldRefAccess<DecontaminationController, DecontaminationController.DecontaminationStatus>("_decontaminationOverride");

    private static readonly AccessTools.FieldRef<DecontaminationController, bool> StopUpdatingRef =
        AccessTools.FieldRefAccess<DecontaminationController, bool>("_stopUpdating");

    private static readonly MethodInfo FinishDecontaminationMethod =
        AccessTools.Method(typeof(DecontaminationController), "FinishDecontamination");

    public static DecontaminationController Controller => DecontaminationController.Singleton;
    public static bool Begun => Controller.IsDecontaminating;
    public static bool InProgress => DecontaminationBegunRef(Controller);

    public static DecontaminationController.DecontaminationStatus Status
    {
        get => DecontaminationOverrideRef(Controller);
        set => DecontaminationOverrideRef(Controller) = value;
    }

    public static bool Locked
    {
        get => StopUpdatingRef(Controller);
        set => StopUpdatingRef(Controller) = value;
    }

    public static void InstantStart()
    {
        FinishDecontaminationMethod.Invoke(Controller, null);
    }
}