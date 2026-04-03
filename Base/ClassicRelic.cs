using MegaCrit.Sts2.Core.Models;

namespace ClassicModeMod;

/// <summary>Base relic for all Classic Mode relics.</summary>
public abstract class ClassicRelic(string assetName) : RelicModel
{
    public override string PackedIconPath => $"res://images/relics/classic/{assetName}.png";

    protected override string PackedIconOutlinePath =>
        $"res://images/relics/classic/outline/{assetName}.png";

    protected override string BigIconPath => PackedIconPath;
}
