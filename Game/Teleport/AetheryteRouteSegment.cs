namespace OmniToolbox.Teleport;

public readonly record struct AetheryteRouteSegment
(
    AetheryteRouteSegmentKind Kind,
    uint                      AetheryteID,
    string                    Name,
    bool                      CanTeleportDirectly
);
