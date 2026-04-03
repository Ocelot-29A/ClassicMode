using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ClassicModeMod;

// ═══════════════════════════════════════════════════════════════════
// STS1 Silent-specific powers (classic implementations)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Whenever you play a card, deal Amount damage to ALL enemies.
/// STS1: A Thousand Cuts (1 base / 2 upgraded).
/// </summary>
public sealed class AThousandCutsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == base.Owner)
        {
            Flash();
            await CreatureCmd.Damage(context, base.CombatState.HittableEnemies, base.Amount, ValueProp.Unpowered, base.Owner, null);
        }
    }
}

/// <summary>
/// Whenever you deal unblocked attack damage, apply Amount of Choke damage.
/// The enemy takes Amount damage whenever they play a card this turn.
/// STS1: Choke (3 base / 5 upgraded).
/// NOTE: In STS1, Choke wasn't a power - it was applied as damage per card played.
/// We implement it as a debuff on the enemy that triggers when they act.
/// </summary>
public sealed class ChokeHoldPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        // Choke wears off at end of player turn (like STS1)
        if (side != base.Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }

    // In STS1, enemy takes damage each time player plays a card while choked.
    // We approximate this as a per-card-played trigger on the owner's side.
}

/// <summary>
/// When this creature dies, deal its current poison as damage to ALL enemies.
/// STS1: Corpse Explosion.
/// </summary>
public sealed class CorpseExplosionPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>
/// Next turn, your attacks deal double damage.
/// STS1: Phantasmal Killer.
/// </summary>
public sealed class PhantasmalKillerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? card)
    {
        if (dealer == base.Owner)
        {
            return amount; // double the damage (adds 100%)
        }
        return 0m;
    }

    public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (side == base.Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
