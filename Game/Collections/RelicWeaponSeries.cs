using System.Collections.Frozen;

namespace OmniToolbox.Collections;

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
