using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace ClassicModeMod;

[HarmonyPatch(typeof(ModelDb), "get_AllRelics")]
internal static class ModelDbAllRelicsPatch
{
    static void Postfix(ref IEnumerable<RelicModel> __result)
    {
        if (__result == null)
            return;
        if (!ClassicConfig.AddClassicRelics && !ClassicConfig.OnlyClassicRelics)
            return;

        __result = __result
            .Concat(ModelDb.RelicPool<ClassicSharedRelicPool>().AllRelics)
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .OrderBy(r => r.Id.Entry);
    }
}
