using System.Collections.Frozen;

namespace OmniToolbox.Collections;

internal readonly record struct RelicMaterialRequirement
(
    int                StageIndex,
    uint               ItemID,
    int                Quantity,
    string             QuantityText,
    string             MaximumQuantityText,
    uint               JobID,
    RelicMaterialScope Scope,
    string             LabelKey,
    int                FreeWeapons
)
{
    public uint CostTargetItemID { get; init; }

    public int CostPerTargetItem { get; init; }
}

internal enum RelicMaterialScope
{
    PerWeapon,
    OncePerSeries
}

internal enum RelicWeaponJobOrder
{
    Legacy,
    Current,
    Life,
    Ultimate
}

internal sealed class RelicWeaponSeries
{
    public required RelicWeaponSeriesKind Kind { get; init; }

    public required string LabelKey { get; init; }

    public required string[] StageLabelKeys { get; init; }

    public required uint[] JobIds { get; init; }

    public required uint[][] StageItemIds { get; init; }

    public uint[] ShieldItemIds { get; init; } = [];

    public required RelicWeaponJobOrder JobOrder { get; init; }

    public RelicMaterialRequirement[] Materials { get; init; } = [];

    public uint[] TotemItemIds { get; init; } = [];

    public bool ExportReplicas { get; init; }
}

public enum RelicWeaponSeriesKind
{
    Ultimate,
    Elegant,
    Dream,
    Phantom,
    Mandervillous,
    Bozja,
    Eureka,
    Anima,
    Zodiac,
    DeepDungeon,
    Primal,
    Yokai,
    Cosmos,
    Splendorous,
    Skysteel,
    Huihuang,
    Cifu,
    Huaguang,
    Liuguang,
    Lockblade,
    Garo,
    Hellhound,
    Triumph,
    Lightning,
    AdvancedAnimal
}
