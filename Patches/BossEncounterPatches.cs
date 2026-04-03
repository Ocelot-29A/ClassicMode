using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;

namespace ClassicModeMod;

/// <summary>
/// Adds Hexaghost as a boss encounter in Act 1 (Overgrowth).
/// Uses Postfix to append to the existing encounter list.
/// </summary>
[HarmonyPatch(typeof(Overgrowth), nameof(Overgrowth.GenerateAllEncounters))]
internal static class OvergrowthBossEncounterPatch
{
    static void Postfix(ref IEnumerable<EncounterModel> __result)
    {
        __result = __result.Append(ModelDb.Encounter<HexaghostBoss>());
    }
}
