using MessagePack;

namespace OmniToolbox.Data;

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class ItemCatalogData
{
    [Key(0)] public ItemSupplementRow[] ItemSupplements { get; set; } = [];
    [Key(1)] public ItemPatchRow[] PatchRanges { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class ItemSupplementRow
{
    [Key(0)] public uint ItemID { get; set; }
    [Key(1)] public uint SourceItemID { get; set; }
    [Key(2)] public uint Source { get; set; }
    [Key(3)] public uint? Min { get; set; }
    [Key(4)] public uint? Max { get; set; }
    [Key(5)] public decimal? Probability { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class ItemPatchRow
{
    [Key(0)] public uint StartItemID { get; set; }
    [Key(1)] public uint EndItemID { get; set; }
    [Key(2)] public decimal Patch { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class WikiItemData
{
    [Key(0)] public int SchemaVersion { get; set; }
    [Key(1)] public string Source { get; set; } = string.Empty;
    [Key(2)] public string GeneratedAtUTC { get; set; } = string.Empty;
    [Key(3)] public int ItemCount { get; set; }
    [Key(4)] public WikiPatchRow[] Patches { get; set; } = [];
    [Key(5)] public uint[] UnobtainableItemIDs { get; set; } = [];
    [Key(6)] public WikiSourceRow[] Sources { get; set; } = [];
    [Key(7)] public WikiItemRow[] Items { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class WikiPatchRow
{
    [Key(0)] public uint ItemID { get; set; }
    [Key(1)] public decimal Patch { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class WikiItemRow
{
    [Key(0)] public uint ItemID { get; set; }
    [Key(1)] public int[] SourceIndices { get; set; } = [];
}

[Flags]
internal enum WikiSourceFields : byte
{
    None = 0,
    MapID = 1,
    X = 2,
    Y = 4,
    RequiredQuests = 8,
    Costs = 16,
    GilShop = 32
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class WikiSourceRow
{
    [Key(0)] public int Category { get; set; }
    [Key(1)] public uint SourceID { get; set; }
    [Key(2)] public string? Name { get; set; }
    [Key(3)] public string? Detail { get; set; }
    [Key(4)] public uint? MapID { get; set; }
    [Key(5)] public decimal? X { get; set; }
    [Key(6)] public decimal? Y { get; set; }
    [Key(7)] public uint[]? RequiredQuestIDs { get; set; }
    [Key(8)] public uint[]? CostItemIDs { get; set; }
    [Key(9)] public bool? IsGilShop { get; set; }
    [Key(10)] public WikiSourceFields Fields { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class DungeonSourceData
{
    [Key(0)] public DungeonBossRow[] Bosses { get; set; } = [];
    [Key(1)] public DungeonChestRow[] Chests { get; set; } = [];
    [Key(2)] public DungeonBossDropRow[] BossDrops { get; set; } = [];
    [Key(3)] public DungeonBossChestRow[] BossChests { get; set; } = [];
    [Key(4)] public DungeonChestItemRow[] ChestItems { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class DungeonBossRow
{
    [Key(0)] public uint RowID { get; set; }
    [Key(1)] public uint BNPCNameID { get; set; }
    [Key(2)] public uint ContentFinderConditionID { get; set; }
    [Key(3)] public uint FightNo { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class DungeonChestRow
{
    [Key(0)] public uint RowID { get; set; }
    [Key(1)] public uint ChestNo { get; set; }
    [Key(2)] public uint ContentFinderConditionID { get; set; }
    [Key(3)] public uint MapID { get; set; }
    [Key(4)] public uint TerritoryTypeID { get; set; }
    [Key(5)] public uint TreasureID { get; set; }
    [Key(6)] public uint DungeonBossID { get; set; }
    [Key(7)] public string Position { get; set; } = string.Empty;
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class DungeonBossDropRow
{
    [Key(0)] public uint RowID { get; set; }
    [Key(1)] public uint ContentFinderConditionID { get; set; }
    [Key(2)] public uint FightNo { get; set; }
    [Key(3)] public uint ItemID { get; set; }
    [Key(4)] public uint Quantity { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class DungeonBossChestRow
{
    [Key(0)] public uint RowID { get; set; }
    [Key(1)] public uint ItemID { get; set; }
    [Key(2)] public uint ContentFinderConditionID { get; set; }
    [Key(3)] public uint Quantity { get; set; }
    [Key(4)] public uint FightNo { get; set; }
    [Key(5)] public uint CofferNo { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed partial class DungeonChestItemRow
{
    [Key(0)] public uint RowID { get; set; }
    [Key(1)] public uint ItemID { get; set; }
    [Key(2)] public uint ChestID { get; set; }
    [Key(3)] public uint Min { get; set; }
    [Key(4)] public uint Max { get; set; }
    [Key(5)] public decimal Probability { get; set; }
}

internal sealed class GameDataPackageIndex
{
    public int SchemaVersion { get; set; } = 1;
    public int GeneratorVersion { get; set; } = 1;
    public Dictionary<string, GameDataResourceInfo> Resources { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Groups { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, GameDataPackageInfo> Packages { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class GameDataResourceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string SHA256 { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public string[]? Header { get; set; }
    public string? GeneratedAtUTC { get; set; }
}

internal sealed class GameDataPackageInfo
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SHA256 { get; set; } = string.Empty;
    public string InputHash { get; set; } = string.Empty;
    public string[] Inputs { get; set; } = [];
}
