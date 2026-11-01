namespace OmniToolbox.Teleport;

public readonly record struct AetheryteDestination
(
    uint   ID,
    string Name,
    string MapName,
    bool   CanTeleportDirectly
);
