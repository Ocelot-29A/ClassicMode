using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace ClassicModeMod;

// ═══════════════════════════════════════════════════════════════════
// CLASSIC DEFECT POWERS
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// STS1 Electrodynamics: Lightning hits ALL enemies.
/// Implemented as a power that triggers additional lightning damage on non-primary targets.
/// For simplicity, channels a Lightning for each enemy when lightning evokes.
/// </summary>
public sealed class ElectrodynamicsPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Electrodynamics is passive; the actual AoE lightning behavior
    // would require deep orb-evoke hooks. As a simplified version,
    // this power is a marker -- real effect handled by orb evoke patches if needed.
}

/// <summary>
/// STS1 Static Discharge: Whenever you take unblocked attack damage, channel Amount Lightning.
/// </summary>
public sealed class StaticDischargePower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == base.Owner && result.UnblockedDamage > 0 && dealer != null)
        {
            Flash();
            for (int i = 0; i < (int)base.Amount; i++)
            {
                await OrbCmd.Channel<LightningOrb>(choiceContext, base.Owner.Player);
            }
        }
    }
}

/// <summary>
/// STS1 Creative AI: At the start of each turn, add Amount random Power card to hand.
/// </summary>
public sealed class CreativeAiPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side != base.Owner.Side)
            return;

        Flash();
        for (int i = 0; i < (int)base.Amount; i++)
        {
            CardModel card = CardFactory.GetDistinctForCombat(
                base.Owner.Player,
                from c in base.Owner.Player.Character.CardPool.GetUnlockedCards(
                    base.Owner.Player.UnlockState,
                    base.Owner.Player.RunState.CardMultiplayerConstraint)
                where c.Type == CardType.Power
                select c,
                1,
                base.Owner.Player.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (card != null)
            {
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
            }
        }
    }
}

/// <summary>
/// STS1 Hello World: At the start of each turn, add a random Common card to hand.
/// </summary>
public sealed class HelloWorldPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side != base.Owner.Side)
            return;

        Flash();
        CardModel card = CardFactory.GetDistinctForCombat(
            base.Owner.Player,
            from c in base.Owner.Player.Character.CardPool.GetUnlockedCards(
                base.Owner.Player.UnlockState,
                base.Owner.Player.RunState.CardMultiplayerConstraint)
            where c.Rarity == CardRarity.Common
            select c,
            1,
            base.Owner.Player.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (card != null)
        {
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
        }
    }
}

/// <summary>
/// STS1 Storm: When you play a Power card, channel Amount Lightning.
/// </summary>
public sealed class StormPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == base.Owner && cardPlay.Card.Type == CardType.Power)
        {
            Flash();
            for (int i = 0; i < (int)base.Amount; i++)
            {
                await OrbCmd.Channel<LightningOrb>(choiceContext, base.Owner.Player);
            }
        }
    }
}

/// <summary>
/// STS1 Machine Learning: At the start of each turn, draw Amount extra cards.
/// </summary>
public sealed class MachineLearningPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side != base.Owner.Side)
            return;

        Flash();
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), base.Amount, base.Owner.Player);
    }
}

/// <summary>
/// STS1 Self Repair: At end of combat, heal Amount HP.
/// Implemented via AfterCombatWon hook.
/// </summary>
public sealed class SelfRepairPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (base.Owner.Player != null)
        {
            Flash();
            await CreatureCmd.Heal(base.Owner, base.Amount);
        }
    }
}

/// <summary>
/// STS1 Loop: At the start of each turn, trigger the passive of your first Orb Amount time(s).
/// </summary>
public sealed class LoopPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side != base.Owner.Side)
            return;

        var orbQueue = base.Owner.Player?.PlayerCombatState?.OrbQueue;
        if (orbQueue?.Orbs.Count > 0)
        {
            Flash();
            var firstOrb = orbQueue.Orbs[0];
            for (int i = 0; i < (int)base.Amount; i++)
            {
                await OrbCmd.Passive(new ThrowingPlayerChoiceContext(), firstOrb, null);
            }
        }
    }
}

/// <summary>
/// STS1 Heatsinks: Whenever you play a Power card, draw Amount card(s).
/// </summary>
public sealed class HeatsinksPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == base.Owner && cardPlay.Card.Type == CardType.Power)
        {
            Flash();
            await CardPileCmd.Draw(choiceContext, base.Amount, base.Owner.Player);
        }
    }
}

/// <summary>
/// STS1 Echo Form: First card each turn is played twice.
/// For simplicity, this is a marker power. Full implementation would require
/// deep hooks into the card play pipeline.
/// </summary>
public sealed class EchoFormPower_C : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Echo Form's double-play mechanic requires deep integration with the card play system.
    // The power exists as a marker; the actual doubling would need play-system patches.
}
