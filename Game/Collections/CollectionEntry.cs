using OmniToolbox.Items;

namespace OmniToolbox.Collections;

public sealed record CollectionEntry
{
    public required uint ID { get; init; }

    public required string Name { get; init; }

    public required uint IconID { get; init; }

    public required CollectionType Type { get; init; }

    public uint? ItemID { get; init; }

    public required decimal Patch { get; init; }

    public required string Description { get; init; }

    public required string Detail { get; init; }

    public required bool IsTradable { get; init; }

    public required bool CanTrade { get; init; }

    public required ItemAvailability Availability { get; init; }

    public required uint ItemLevel { get; init; }

    public required IReadOnlyList<CollectionSource> Sources { get; init; }

    public required bool IsUnlocked { get; init; }

    public bool IsUnlockStateKnown { get; init; } = true;

    public ulong Key => (ulong)Type << 32 | ID;
}
