using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace ClassicModeMod;

// Hybrid pools merge STS2 base cards with STS1 (Classic) cards, so reward offers
// draw from both sets. Visual identity matches the STS2 base pool.
//
// ModelId dedupe: STS1 cards use `_C`-suffixed types with distinct ids, so they
// normally coexist with STS2 cards (e.g. Bash and Bash_C both appear).
//
// Optional display-name dedupe (ClassicConfig.HybridDedupe): when on, STS2 wins
// and any STS1 card whose Title collides with an already-added STS2 card is
// dropped. Cache invalidation when the toggle flips lives in HybridPoolCache.

internal static class HybridPoolHelper
{
    internal static CardModel[] MergeCards(CardPoolModel sts2, CardPoolModel classic)
    {
        var seenIds = new HashSet<ModelId>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dedupeByName = ClassicConfig.HybridDedupe;
        var merged = new List<CardModel>();

        // STS2 first so that on name-collision it wins.
        foreach (var c in sts2.AllCards)
        {
            if (!seenIds.Add(c.Id)) continue;
            if (dedupeByName) seenNames.Add(c.Title);
            merged.Add(c);
        }
        foreach (var c in classic.AllCards)
        {
            if (!seenIds.Add(c.Id)) continue;
            if (dedupeByName && !seenNames.Add(c.Title)) continue;
            merged.Add(c);
        }
        return merged.ToArray();
    }

    internal static RelicModel[] MergeRelics(RelicPoolModel sts2, RelicPoolModel classic)
    {
        var seenIds = new HashSet<ModelId>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dedupeByName = ClassicConfig.HybridDedupe;
        var merged = new List<RelicModel>();

        foreach (var r in sts2.AllRelics)
        {
            if (!seenIds.Add(r.Id)) continue;
            if (dedupeByName) seenNames.Add(r.Title.GetFormattedText());
            merged.Add(r);
        }
        foreach (var r in classic.AllRelics)
        {
            if (!seenIds.Add(r.Id)) continue;
            if (dedupeByName && !seenNames.Add(r.Title.GetFormattedText())) continue;
            merged.Add(r);
        }
        return merged.ToArray();
    }
}

public sealed class HybridIroncladCardPool : CardPoolModel
{
    public override string Title => "ironclad";
    public override string EnergyColorName => "ironclad";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color DeckEntryCardColor => new("D62000");
    public override Color EnergyOutlineColor => new("802020");
    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
        HybridPoolHelper.MergeCards(
            ModelDb.CardPool<IroncladCardPool>(),
            ModelDb.CardPool<ClassicIroncladCardPool>());
}

public sealed class HybridSilentCardPool : CardPoolModel
{
    public override string Title => "silent";
    public override string EnergyColorName => "silent";
    public override string CardFrameMaterialPath => "card_frame_green";
    public override Color DeckEntryCardColor => new("70CC83");
    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
        HybridPoolHelper.MergeCards(
            ModelDb.CardPool<SilentCardPool>(),
            ModelDb.CardPool<ClassicSilentCardPool>());
}

public sealed class HybridDefectCardPool : CardPoolModel
{
    public override string Title => "defect";
    public override string EnergyColorName => "defect";
    public override string CardFrameMaterialPath => "card_frame_blue";
    public override Color DeckEntryCardColor => new("5CC1FF");
    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
        HybridPoolHelper.MergeCards(
            ModelDb.CardPool<DefectCardPool>(),
            ModelDb.CardPool<ClassicDefectCardPool>());
}
