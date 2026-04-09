using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace ClassicModeMod;

/// <summary>
/// Hexaghost - STS1 Act 1 Boss ported to STS2.
/// A ghostly entity with 6 orbiting fire orbs that charges up to Inferno.
/// Deterministic move cycle: Activate → Inferno → Sear → FireTackle → Sear → Inflame → FireTackle → Sear → Inferno (repeat)
/// </summary>
public sealed class Hexaghost : MonsterModel
{
    // --- HP ---
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 264, 250);
    public override int MaxInitialHp => MinInitialHp;

    // --- Damage values ---
    private int SearDamage => 6;
    private int SearBurnCount => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);

    private int FireTackleDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);
    private const int FireTackleHits = 2;

    private int InflameBlock => 12;
    private int InflameStrength => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private int InfernoDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);
    private const int InfernoHits = 6;
    internal const int UpgradedBurnDamage = 4;

    // --- Visuals ---
    public override LocString Title => L10NMonsterLookup("Hexaghost.name");

    protected override string VisualsPath => SceneHelper.GetScenePath("creature_visuals/hexaghost");

    public override float DeathAnimLengthOverride => 1.2f;
    public override bool ShouldFadeAfterDeath => true;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    // No spine, so no custom animator needed. The default GenerateAnimator will be skipped
    // because HasSpineAnimation returns false for Node2D-based visuals.

    // --- Orb tracking ---
    private int _orbActiveCount;
    private bool _burnUpgraded;
    private Node2D[]? _orbNodes;
    private Node2D? _bodyNode;

    // --- Orb positions (hexagon around center) ---
    private static readonly Vector2[] OrbOffsets =
    [
        new(-90, -70),   // top-left
        new(90, -70),    // top-right
        new(160, 100),   // right
        new(90, 270),    // bottom-right
        new(-90, 270),   // bottom-left
        new(-160, 100),  // left
    ];

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        // Activate: first turn, all orbs light up (hidden intent - no damage)
        var activate = new MoveState("ACTIVATE", ActivateMove, new HiddenIntent());

        // Inferno: 2(3) × 6 hits (unique IDs required by state machine dictionary)
        var inferno1 = new MoveState("INFERNO_MOVE_1", InfernoMove, new MultiAttackIntent(InfernoDamage, InfernoHits));
        var inferno2 = new MoveState("INFERNO_MOVE_2", InfernoMove, new MultiAttackIntent(InfernoDamage, InfernoHits));

        // Sear: 6 damage + Burn cards
        var sear1 = new MoveState("SEAR_MOVE_1", SearMove, new SingleAttackIntent(SearDamage), new StatusIntent(SearBurnCount));
        var sear2 = new MoveState("SEAR_MOVE_2", SearMove, new SingleAttackIntent(SearDamage), new StatusIntent(SearBurnCount));
        var sear3 = new MoveState("SEAR_MOVE_3", SearMove, new SingleAttackIntent(SearDamage), new StatusIntent(SearBurnCount));

        // Fire Tackle: 5(6) × 2 hits
        var fireTackle1 = new MoveState("FIRE_TACKLE_MOVE_1", FireTackleMove, new MultiAttackIntent(FireTackleDamage, FireTackleHits));
        var fireTackle2 = new MoveState("FIRE_TACKLE_MOVE_2", FireTackleMove, new MultiAttackIntent(FireTackleDamage, FireTackleHits));

        // Inflame: block + strength (defend + buff intents)
        var inflame = new MoveState("INFLAME_MOVE", InflameMove, new DefendIntent(), new BuffIntent());

        // Chain: Activate → Inferno1 → Sear1 → FireTackle1 → Sear2 → Inflame → FireTackle2 → Sear3 → Inferno2 → (loop to Sear1)
        activate.FollowUpState = inferno1;
        inferno1.FollowUpState = sear1;
        sear1.FollowUpState = fireTackle1;
        fireTackle1.FollowUpState = sear2;
        sear2.FollowUpState = inflame;
        inflame.FollowUpState = fireTackle2;
        fireTackle2.FollowUpState = sear3;
        sear3.FollowUpState = inferno2;
        inferno2.FollowUpState = sear1; // loop

        states.Add(activate);
        states.Add(inferno1);
        states.Add(inferno2);
        states.Add(sear1);
        states.Add(sear2);
        states.Add(sear3);
        states.Add(fireTackle1);
        states.Add(fireTackle2);
        states.Add(inflame);

        return new MonsterMoveStateMachine(states, activate);
    }

    // ===================== MOVES =====================

    private async Task ActivateMove(IReadOnlyList<Creature> targets)
    {
        // All 6 orbs activate
        _orbActiveCount = 6;
        SetAllOrbsActive(true);

        // Visual: brief pause for dramatic effect
        SfxCmd.Play("event:/sfx/characters/attack_fire");
        await Cmd.Wait(0.8f);
    }

    private async Task InfernoMove(IReadOnlyList<Creature> targets)
    {
        // Screen-wide fire attack
        SfxCmd.Play("event:/sfx/characters/attack_fire");

        await DamageCmd.Attack(InfernoDamage)
            .WithHitCount(InfernoHits)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_fire")
            .Execute(null);

        // After Inferno: deactivate all orbs and upgrade all Burns for the rest of combat
        _orbActiveCount = 0;
        _burnUpgraded = true;
        SetAllOrbsActive(false);
        await UpgradeExistingBurns(targets);
    }

    private async Task SearMove(IReadOnlyList<Creature> targets)
    {
        SfxCmd.Play("event:/sfx/characters/attack_fire");

        await DamageCmd.Attack(SearDamage)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_fire")
            .Execute(null);

        // Add Burn cards to discard pile
        await AddBurnsToDiscard(targets, SearBurnCount);

        // Activate one orb
        ActivateNextOrb();
    }

    private async Task FireTackleMove(IReadOnlyList<Creature> targets)
    {
        SfxCmd.Play("event:/sfx/characters/attack_fire");

        await DamageCmd.Attack(FireTackleDamage)
            .WithHitCount(FireTackleHits)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        // Activate one orb
        ActivateNextOrb();
    }

    private async Task InflameMove(IReadOnlyList<Creature> targets)
    {
        SfxCmd.Play("event:/sfx/characters/attack_fire");

        await CreatureCmd.GainBlock(base.Creature, InflameBlock, ValueProp.Move, null);
        await PowerCmd.Apply<StrengthPower>(base.Creature, InflameStrength, base.Creature, null);

        // Activate one orb
        ActivateNextOrb();
    }

    private async Task AddBurnsToDiscard(IReadOnlyList<Creature> targets, int count)
    {
        if (!_burnUpgraded)
        {
            await CardPileCmd.AddToCombatAndPreview<Burn>(targets, PileType.Discard, count, addedByPlayer: false);
            return;
        }

        var addedCards = new List<CardPileAddResult>(count * targets.Count);
        foreach (Creature target in targets)
        {
            Player? owner = target.Player ?? target.PetOwner;
            if (owner == null)
                continue;

            for (int i = 0; i < count; i++)
            {
                HexaghostBurnPlus? burn = target.CombatState?.CreateCard<HexaghostBurnPlus>(owner);
                if (burn == null)
                    continue;

                addedCards.Add(await CardPileCmd.Add(burn, PileType.Discard));
            }
        }

        if (addedCards.Count > 0)
        {
            CardCmd.PreviewCardPileAdd(
                addedCards.ToArray(),
                1.2f,
                addedCards.Count <= 5 ? CardPreviewStyle.HorizontalLayout : CardPreviewStyle.MessyLayout);
        }
    }

    private async Task UpgradeExistingBurns(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            Player? owner = target.Player ?? target.PetOwner;
            if (owner == null)
                continue;

            List<CardModel> burnsToUpgrade = CardPile.GetCards(owner, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play)
                .Where(card => card is Burn)
                .ToList();
            foreach (CardModel burn in burnsToUpgrade)
            {
                await CardCmd.TransformTo<HexaghostBurnPlus>(burn, CardPreviewStyle.None);
            }
        }
    }

    // ===================== ORB VISUALS =====================

    private void ActivateNextOrb()
    {
        if (_orbActiveCount < 6)
        {
            SetOrbActive(_orbActiveCount, true);
            _orbActiveCount++;
        }
    }

    private void SetAllOrbsActive(bool active)
    {
        if (_orbNodes == null) return;
        for (int i = 0; i < 6; i++)
            SetOrbActive(i, active);
    }

    private void SetOrbActive(int index, bool active)
    {
        if (_orbNodes == null || index < 0 || index >= _orbNodes.Length) return;
        var orb = _orbNodes[index];
        if (orb == null) return;

        // Active: full brightness, scale up; Inactive: dim, scale down
        orb.Modulate = active
            ? new Color(0.5f, 1.0f, 0.0f, 1.0f) // chartreuse
            : new Color(0.3f, 0.5f, 0.0f, 0.3f); // dim
        orb.Scale = active ? Vector2.One * 1.0f : Vector2.One * 0.6f;
    }

    // ===================== VISUAL SETUP =====================

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(base.Creature);
        if (creatureNode == null) return;

        // Access the Visuals child node (the %Visuals unique name node in the .tscn)
        var body = creatureNode.Visuals.GetNode<Node2D>("Visuals");

        // Load textures from base game assets
        var coreTex = LoadTexture("res://images/monsters/theBottom/boss/ghost/core.png");
        var plasma1Tex = LoadTexture("res://images/monsters/theBottom/boss/ghost/plasma1.png");
        var plasma2Tex = LoadTexture("res://images/monsters/theBottom/boss/ghost/plasma2.png");
        var plasma3Tex = LoadTexture("res://images/monsters/theBottom/boss/ghost/plasma3.png");
        var shadowTex = LoadTexture("res://images/monsters/theBottom/boss/ghost/shadow.png");
        var fireTex = LoadTexture("res://images/monsters/theBottom/boss/fire1.png");

        // Shadow (behind everything)
        if (shadowTex != null)
        {
            var shadow = new Sprite2D { Texture = shadowTex, Position = new Vector2(0, 15), Modulate = new Color(1, 1, 1, 0.5f) };
            body.AddChild(shadow);
        }

        // Plasma rings (rotating body)
        _bodyNode = new Node2D { Name = "PlasmaBody" };
        body.AddChild(_bodyNode);

        if (plasma3Tex != null)
        {
            var p3 = new Sprite2D { Texture = plasma3Tex, Position = new Vector2(0, -12), Modulate = new Color(0.8f, 0.3f, 0.8f, 0.7f) };
            _bodyNode.AddChild(p3);
        }
        if (plasma2Tex != null)
        {
            var p2 = new Sprite2D { Texture = plasma2Tex, Position = new Vector2(0, -6), Modulate = new Color(0.7f, 0.2f, 0.7f, 0.8f) };
            _bodyNode.AddChild(p2);
        }
        if (plasma1Tex != null)
        {
            var p1 = new Sprite2D { Texture = plasma1Tex, Modulate = new Color(0.6f, 0.1f, 0.6f, 0.9f) };
            _bodyNode.AddChild(p1);
        }

        // Core (center)
        if (coreTex != null)
        {
            var core = new Sprite2D { Texture = coreTex, ZIndex = 1 };
            body.AddChild(core);
        }

        // Fire orbs (6 around the hexagon)
        _orbNodes = new Node2D[6];
        for (int i = 0; i < 6; i++)
        {
            var orbContainer = new Node2D
            {
                Name = $"Orb{i}",
                Position = OrbOffsets[i]
            };

            if (fireTex != null)
            {
                var fireSprite = new Sprite2D
                {
                    Texture = fireTex,
                    Scale = new Vector2(0.4f, 0.4f)
                };
                orbContainer.AddChild(fireSprite);
            }

            body.AddChild(orbContainer);
            _orbNodes[i] = orbContainer;

            // Start all orbs inactive
            SetOrbActive(i, false);
        }

        // Start rotation animation for plasma body
        StartPlasmaRotation(body);
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (!ResourceLoader.Exists(path)) return null;
        return ResourceLoader.Load<Texture2D>(path);
    }

    private void StartPlasmaRotation(Node2D body)
    {
        if (_bodyNode == null) return;

        // Use a looping tween for continuous rotation
        var tween = body.CreateTween();
        tween.SetLoops(); // infinite
        tween.TweenProperty(_bodyNode, "rotation", Mathf.Tau, 60.0f); // full rotation in 60s (slow ambient)
    }
}

public sealed class HexaghostBurnPlus : CardModel
{
    public override int MaxUpgradeLevel => 0;
    public override CardPoolModel Pool => ModelDb.CardPool<StatusCardPool>();
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<StatusCardPool>();
    public override string PortraitPath => ModelDb.Card<Burn>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<Burn>().BetaPortraitPath;
    public override IEnumerable<string> AllPortraitPaths => ModelDb.Card<Burn>().AllPortraitPaths;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(Hexaghost.UpgradedBurnDamage, ValueProp.Unpowered | ValueProp.Move)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    public override bool HasTurnEndInHandEffect => true;
    protected override IEnumerable<string> ExtraRunAssetPaths => NGroundFireVfx.AssetPaths;

    public HexaghostBurnPlus()
        : base(-1, CardType.Status, CardRarity.Status, TargetType.None)
    {
    }

    public override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NGroundFireVfx.Create(Owner.Creature));
        SfxCmd.Play("event:/sfx/characters/attack_fire");
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.Damage, this);
    }
}
