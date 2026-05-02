using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace ClassicModeMod;

[HarmonyPatch(typeof(ModelDb), "get_AllRelicPools")]
internal static class ModelDbAllRelicPoolsPatch
{
    static void Postfix(ref IEnumerable<RelicPoolModel> __result)
    {
        if (__result == null)
            return;
        if (!ClassicConfig.AddClassicRelics && !ClassicConfig.OnlyClassicRelics)
            return;

        RelicPoolModel classicShared = ModelDb.RelicPool<ClassicSharedRelicPool>();
        List<RelicPoolModel> pools = __result.ToList();
        if (pools.Any(p => p.Id == classicShared.Id))
        {
            __result = pools;
            return;
        }

        pools.Add(classicShared);
        __result = pools;
    }
}
