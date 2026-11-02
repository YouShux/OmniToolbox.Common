using OmniToolbox.Items;

namespace OmniToolbox.Collections;

public readonly record struct CollectionSource
(
    CollectionSourceCategory  Category,
    uint                      SourceID,
    string                    Description,
    string                    Detail,
    CollectionSourceLocation? Location = null
)
{
    public uint MapTerritoryID { get; init; }

    public uint TargetItemID { get; init; }

    internal IReadOnlyList<uint> CostItemIDs { get; init; } = [];

    internal IReadOnlyList<uint> RequiredQuestIDs { get; init; } = [];

    internal string RequiredQuestDetail { get; init; } = string.Empty;

    internal string SourceDetail { get; init; } = string.Empty;

    internal bool IsIdentityFallback { get; init; }

    internal bool IsGilShop { get; init; }
}
