namespace OmniToolbox.Collections;

public readonly record struct CollectionSourceLocation
(
    uint  TerritoryID,
    float X,
    float Y
)
{
    public uint MapID { get; init; }
}
