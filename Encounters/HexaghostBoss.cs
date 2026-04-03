using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace ClassicModeMod;

/// <summary>
/// Boss encounter for Hexaghost - STS1 Act 1 Boss.
/// Single monster encounter with no custom scene or background.
/// </summary>
public sealed class HexaghostBoss : EncounterModel
{
    public override RoomType RoomType => RoomType.Boss;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Hexaghost>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return [(ModelDb.Monster<Hexaghost>().ToMutable(), null)];
    }

    // Use the existing hexaghost map icon from the base game
    public override string BossNodePath => "res://images/ui/map/boss/hexaghost";
}
