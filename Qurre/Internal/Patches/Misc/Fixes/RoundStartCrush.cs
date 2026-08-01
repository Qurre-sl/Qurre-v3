using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using PlayerRoles;
using PlayerRoles.RoleAssign;
using Log = Qurre.API.Log;

namespace Qurre.Internal.Patches.Misc.Fixes;

[HarmonyPatch(typeof(HumanSpawner), "AssignHumanRoleToRandomPlayer")]
internal static class RoundStartCrush
{
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> Call(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0); // RoleTypeId role
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RoundStartCrush), nameof(Invoke)));
        yield return new CodeInstruction(OpCodes.Ret);
    }
    
    private static readonly Type RoleHistoryType = AccessTools.Inner(typeof(HumanSpawner), "RoleHistory");

    private static readonly FieldInfo CandidatesField = AccessTools.Field(typeof(HumanSpawner), "Candidates");
    private static readonly FieldInfo HistoryField = AccessTools.Field(typeof(HumanSpawner), "History");
    
    private static readonly PropertyInfo RoleHistoryProperty = AccessTools.Property(RoleHistoryType, "History");
    private static readonly MethodInfo RegisterRoleMethod = AccessTools.Method(RoleHistoryType, "RegisterRole");
    
    private static void Invoke(RoleTypeId role)
{
    try
    {
        var candidates = (List<ReferenceHub>)CandidatesField.GetValue(null);
        candidates.Clear();

        // Dictionary<string, RoleHistory> — кастуем к IDictionary, чтобы не указывать приватный RoleHistory как generic-параметр
        var history = (IDictionary)HistoryField.GetValue(null);

        int num1 = int.MaxValue;

        foreach (ReferenceHub allHub in ReferenceHub.AllHubs)
        {
            try
            {
                if (!RoleAssigner.CheckPlayer(allHub)) continue;

                string userId = allHub.authManager.UserId;

                object roleHistory;
                if (history.Contains(userId))
                {
                    roleHistory = history[userId];
                }
                else
                {
                    roleHistory = AccessTools.CreateInstance(RoleHistoryType);
                    history[userId] = roleHistory;
                }

                var historyArray = (RoleTypeId[])RoleHistoryProperty.GetValue(roleHistory);

                int num2 = 0;
                for (int index = 0; index < 5; ++index)
                    if (historyArray[index] == role)
                        ++num2;

                if (num2 <= num1)
                {
                    if (num2 < num1)
                        candidates.Clear();
                    candidates.Add(allHub);
                    num1 = num2;
                }
            }
            catch (Exception err)
            {
                Log.Warn(err);
            }
        }

        if (candidates.Count == 0)
            return;

        ReferenceHub referenceHub = candidates.RandomItem();
        referenceHub.roleManager.ServerSetRole(role, RoleChangeReason.RoundStart);

        string finalUserId = referenceHub.authManager.UserId;
        object finalRoleHistory = history[finalUserId];
        RegisterRoleMethod.Invoke(finalRoleHistory, new object[] { role });
    }
    catch (Exception err)
    {
        Log.Warn(err);
    }
}
}