using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ClassicModeMod;

[ModInitializer(nameof(Init))]
public static class ClassicBootstrap
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized)
            return;

        _initialized = true;

        ClassicConfig.Load();
        ClassicModConfigBridge.DeferredRegister();

        var harmony = new Harmony("boninall.classicmode.beta");
        int applied = 0, failed = 0;

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            try
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                    continue;
                var processor = new PatchClassProcessor(harmony, type);
                processor.Patch();
                applied++;
                Log.Info($"[ClassicModeBeta] Patched: {type.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Log.Error($"[ClassicModeBeta] Patch failed for {type.Name}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        Log.Info($"[ClassicModeBeta] Harmony patches: {applied} applied, {failed} failed.");
        Log.Info("[ClassicModeBeta] Classic Mode Beta initialized.");
    }
}
