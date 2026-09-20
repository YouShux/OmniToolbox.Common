using OmniToolbox.Items;

namespace OmniToolbox.Collections;

public enum CollectionType
{
    Equipment = 0,
    Mount = 1,
    Minion = 2,
    Emote = 3,
    Hairstyle = 4,
    TripleTriadCard = 5,
    Barding = 6,
    Orchestrion = 7,
    Portrait = 8,
    FashionAccessory = 9,
    Glasses = 10,
    BlueMageSpell = 11,
    BlindBox = 12,
    Relic = 13,
    Bestiary = 14
}

public enum CollectionSourceCategory
{
    Achievement = 0,
    Container = 1,
    Crafting = 2,
    Duty = 3,
    Event = 4,
    MogStation = 5,
    PvP = 6,
    Quest = 7,
    Submarine = 8,
    Shop = 9,
    Other = 10,
    Gil = 11,
    Scrips = 12,
    Tomestones = 13,
    CompanySeals = 14,
    MGP = 15,
    TheHunt = 16,
    Fate = 17,
    DeepDungeon = 18,
    BeastTribes = 19,
    TreasureHunts = 20,
    RestorationZone = 21,
    FieldOperations = 22,
    IslandSanctuary = 23
}

public enum ItemAvailability
{
    Obtainable,
    Purchasable,
    Unobtainable
}

public readonly record struct CollectionSource(
    CollectionSourceCategory Category,
    uint SourceID,
    string Description,
    string Detail,
    CollectionSourceLocation? Location = null)
{
    public uint MapTerritoryID { get; init; }
}

public readonly record struct CollectionSourceLocation(
    uint TerritoryID,
    float X,
    float Y);

// 青魔来源行，保留怪物名及补全后的坐标供青魔法书导航使用。
public readonly record struct BlueSpellSource(
    string SourceType,
    string MobDescription,
    string LocationDescription,
    float? X,
    float? Y,
    uint? Level,
    string Note);

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
