namespace OmniToolbox.Teleport;

public readonly record struct AetheryteRouteSegment
(
    AetheryteRouteSegmentKind Kind,
    uint                      AetheryteID,
    string                    Name,
    bool                      CanTeleportDirectly
)
{
    public uint TerritoryID { get; init; }

    public uint NextTerritoryID { get; init; }

    public Vector2? BoundaryPosition { get; init; }
}
