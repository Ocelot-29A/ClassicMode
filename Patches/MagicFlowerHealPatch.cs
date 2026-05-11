using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ClassicModeMod;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal), typeof(Creature), typeof(decimal), typeof(bool))]
internal static class MagicFlowerHealPatch
{
    static void Prefix(Creature creature, ref decimal amount)
    {
        if (creature?.Player == null || amount <= 0m || !CombatManager.Instance.IsInProgress)
            return;

        MagicFlowerRelic? relic = creature.Player?.GetRelic<MagicFlowerRelic>();
        if (relic == null)
            return;

        decimal bonus = decimal.Ceiling(amount * 0.5m);
        if (bonus <= 0)
            return;

        relic.Flash();
        amount += bonus;
    }
}
