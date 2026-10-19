using OmniToolbox.Items;

namespace OmniToolbox.Collections;

public readonly record struct CollectionSource(
    CollectionSourceCategory Category,
    uint SourceID,
    string Description,
    string Detail,
    CollectionSourceLocation? Location = null)
{
    public uint MapTerritoryID { get; init; }

    public uint TargetItemID { get; init; }

    internal IReadOnlyList<uint> CostItemIDs { get; init; } = [];

    internal bool IsGilShop { get; init; }
}
