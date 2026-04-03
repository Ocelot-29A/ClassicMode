using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace ClassicModeMod;

// ── Card Pool Patches ──

[HarmonyPatch(typeof(Ironclad), nameof(Ironclad.CardPool), MethodType.Getter)]
internal static class IroncladCardPoolPatch
{
    static bool Prefix(ref CardPoolModel __result)
    {
        if (!ClassicConfig.ClassicCards) return true;
        __result = ModelDb.CardPool<ClassicIroncladCardPool>();
        return false;
    }
}

[HarmonyPatch(typeof(Silent), nameof(Silent.CardPool), MethodType.Getter)]
internal static class SilentCardPoolPatch
{
    static bool Prefix(ref CardPoolModel __result)
    {
        if (!ClassicConfig.ClassicCards) return true;
        __result = ModelDb.CardPool<ClassicSilentCardPool>();
        return false;
    }
}

[HarmonyPatch(typeof(Defect), nameof(Defect.CardPool), MethodType.Getter)]
internal static class DefectCardPoolPatch
{
    static bool Prefix(ref CardPoolModel __result)
    {
        if (!ClassicConfig.ClassicCards) return true;
        __result = ModelDb.CardPool<ClassicDefectCardPool>();
        return false;
    }
}

// ── Relic Pool Patches ──

[HarmonyPatch(typeof(Ironclad), nameof(Ironclad.RelicPool), MethodType.Getter)]
internal static class IroncladRelicPoolPatch
{
    static bool Prefix(ref RelicPoolModel __result)
    {
        if (!ClassicConfig.ClassicRelics) return true;
        __result = ModelDb.RelicPool<ClassicIroncladRelicPool>();
        return false;
    }
}

[HarmonyPatch(typeof(Silent), nameof(Silent.RelicPool), MethodType.Getter)]
internal static class SilentRelicPoolPatch
{
    static bool Prefix(ref RelicPoolModel __result)
    {
        if (!ClassicConfig.ClassicRelics) return true;
        __result = ModelDb.RelicPool<ClassicSilentRelicPool>();
        return false;
    }
}

[HarmonyPatch(typeof(Defect), nameof(Defect.RelicPool), MethodType.Getter)]
internal static class DefectRelicPoolPatch
{
    static bool Prefix(ref RelicPoolModel __result)
    {
        if (!ClassicConfig.ClassicRelics) return true;
        __result = ModelDb.RelicPool<ClassicDefectRelicPool>();
        return false;
    }
}
