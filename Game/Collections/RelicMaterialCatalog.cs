namespace OmniToolbox.Collections;

internal enum RelicMaterialScope
{
    PerWeapon,
    OncePerSeries
}

internal readonly record struct RelicMaterialRequirement(
    int StageIndex,
    uint ItemID,
    int Quantity,
    string QuantityText,
    string MaximumQuantityText,
    uint JobID,
    RelicMaterialScope Scope,
    string LabelKey,
    int FreeWeapons)
{
    public uint CostTargetItemID { get; init; }

    public int CostPerTargetItem { get; init; }
}

internal static class RelicMaterialCatalog
{
    internal static readonly RelicMaterialRequirement[] Zodiac =
    [
        Progress(1, "Collection.Relic.Material.Text.Prototype", "1"),
        Item(1, 6267, 1),
        Cost(1, 28, 15, 6267, 15),
        Item(2, 6268, 3),
        Cost(2, 28, 60, 6268, 20),
        Progress(3, "Collection.Relic.Material.Text.Atma", "1"),
        Progress(4, "Collection.Relic.Material.Text.Books", "9"),
        Cost(4, 28, 900),
        Item(5, 7885, 3),
        Cost(5, 28, 75, 7885, 25),
        Item(5, 7883, 75),
        Cost(5, 28, 1125, 7883, 15),
        Progress(5, "Collection.Relic.Material.Text.Materia", "75"),
        Progress(6, "Collection.Relic.Material.Text.Light", "Collection.Relic.Material.Quantity.Light2000"),
        Item(7, 9539, 4),
        Item(7, 9540, 4),
        Cost(7, 28, 800, 9540, 200),
        Item(7, 9538, 1),
        Item(7, 9510, 1),
        Item(7, 9511, 1),
        Item(7, 9542, 1),
        Item(7, 9512, 1),
        Item(7, 9513, 1),
        Item(7, 9544, 1),
        Item(7, 9516, 1),
        Item(7, 9517, 1),
        Item(7, 9514, 1),
        Item(7, 9515, 1),
        Item(7, 9543, 1),
        Progress(8, "Collection.Relic.Material.Text.Light", string.Empty),
        Cost(8, 28, 600)
    ];

    internal static readonly RelicMaterialRequirement[] Anima =
    [
        Item(0, 13575, 1),
        Item(0, 13576, 1),
        Progress(1, "Collection.Relic.Material.Text.Dungeons", "10"),
        Item(2, 13582, 10),
        Cost(2, 28, 1500, 13582, 150),
        Item(2, 13589, 4),
        Item(2, 13584, 10),
        Cost(2, 28, 1500, 13584, 150),
        Item(2, 13591, 4),
        Item(2, 13588, 10),
        Cost(2, 28, 1500, 13588, 150),
        Item(2, 13595, 4),
        Item(2, 13586, 10),
        Cost(2, 28, 1500, 13586, 150),
        Item(2, 13593, 4),
        Item(3, 14899, 5),
        Cost(3, 28, 1750, 14899, 350),
        Item(4, 15841, 60),
        Item(4, 15840, 60),
        Cost(4, 28, 4500, 15840, 75),
        Item(5, 16064, 50),
        Cost(5, 28, 2000, 16064, 40),
        Progress(6, "Collection.Relic.Material.Text.Light", "Collection.Relic.Material.Quantity.Light1000"),
        Item(6, 16932, 1),
        Cost(6, 28, 1500, 16932, 1500),
        Item(7, 16934, 1),
        Cost(7, 28, 500, 16934, 500)
    ];

    internal static readonly RelicMaterialRequirement[] Eureka =
    [
        Item(0, 21801, 100),
        Item(1, 21801, 400),
        Item(2, 21801, 800),
        Item(3, 21802, 3),
        Item(4, 23309, 5),
        Item(5, 23309, 10),
        Item(5, 22976, 500),
        Item(6, 23309, 16),
        Item(6, 22975, 5),
        Progress(7, "Collection.Relic.Material.Text.Logograms", "10"),
        Item(7, 24124, 150),
        Progress(8, "Collection.Relic.Material.Text.Logograms", "20"),
        Item(8, 24124, 200),
        Progress(9, "Collection.Relic.Material.Text.Logograms", "30"),
        Item(9, 24124, 300),
        Item(9, 24123, 5),
        Item(10, 24807, 50),
        Item(11, 24807, 100),
        Item(12, 24807, 100),
        Item(13, 24807, 100),
        Item(13, 24806, 5),
        Item(14, 24808, 100)
    ];

    internal static readonly RelicMaterialRequirement[] Bozja =
    [
        FreeItem(0, 30273, 4, 1),
        Cost(0, 28, 1000, 30273, 250),
        Item(1, 31573, 20),
        Item(1, 31574, 20),
        Item(1, 31575, 20),
        Item(2, 31576, 6),
        Item(3, 32956, 15),
        Once(4, 32957, 18),
        Once(4, 32958, 18),
        Item(4, 32959, 15),
        Once(5, 33757, 30),
        Once(5, 33758, 30),
        Once(5, 33759, 30),
        Once(5, 33760, 30),
        Once(5, 33763, 30),
        Once(5, 33764, 30),
        Item(5, 33767, 15)
    ];

    internal static readonly RelicMaterialRequirement[] Mandervillous =
    [
        Item(0, 38420, 3),
        Cost(0, 28, 1500, 38420, 500),
        Item(1, 38940, 3),
        Cost(1, 28, 1500, 38940, 500),
        Item(2, 40322, 3),
        Cost(2, 28, 1500, 40322, 500),
        Item(3, 41032, 3),
        Cost(3, 28, 1500, 41032, 500)
    ];

    internal static readonly RelicMaterialRequirement[] Phantom =
    [
        Once(0, 47744, 3),
        Once(0, 47745, 3),
        Once(0, 47746, 3),
        Once(0, 47747, 3),
        Once(0, 47748, 3),
        Once(0, 47749, 3),
        Item(0, 47750, 3),
        Cost(0, 48, 1500, 47750, 500),
        Once(1, 46854, 1),
        Once(1, 46852, 1),
        Cost(1, 26807, 600, 46852, 600, RelicMaterialScope.OncePerSeries),
        Once(1, 46855, 1),
        Once(1, 46856, 1),
        Once(1, 46857, 1),
        Progress(1, "Collection.Relic.Material.Text.Light", "Collection.Relic.Material.Quantity.Each10000", RelicMaterialScope.OncePerSeries),
        Item(1, 46850, 3),
        Cost(1, 48, 1500, 46850, 500),
        Once(2, 50970, 1),
        Once(2, 50972, 1),
        Once(2, 50971, 1),
        Once(2, 50973, 1),
        Once(2, 50059, "Collection.Relic.Material.Quantity.CrystalClay1200"),
        Item(2, 50058, 3),
        Cost(2, 48, 1500, 50058, 500),
        Once(3, 50974, 1),
        Once(3, 50975, 1),
        Once(3, 50976, 1),
        Progress(3, "Collection.Relic.Material.Text.PhantomOrbs", "Collection.Relic.Material.Quantity.Each100", RelicMaterialScope.OncePerSeries),
        Item(3, 50977, 3),
        Cost(3, 48, 1500, 50977, 500),
        Progress(4, "Collection.Relic.Material.Text.BattleMemory", string.Empty, RelicMaterialScope.OncePerSeries),
        Progress(4, "Collection.Relic.Material.Text.NoMaterial", string.Empty)
    ];

    internal static readonly RelicMaterialRequirement[] Skysteel =
    [
        ..Crafting(1, 20, [29661, 29662, 29663, 29664, 29665, 29666, 29667, 29668]),
        ..Crafting(1, 20, [27685, 27699, 27699, 27699, 27734, 27753, 27774, 27968]),
        ..Crafting(2, 30, [29661, 29662, 29663, 29664, 29665, 29666, 29667, 29668]),
        ..Crafting(2, 30, [27692, 27713, 27713, 27804, 27741, 27754, 27777, 27843]),
        ..CraftingRange(3, "18", "45", [31117, 31118, 31119, 31120, 31121, 31122, 31123, 31124]),
        ..CraftingRange(3, "36", "90", [27687, 27703, 27703, 27702, 27817, 27759, 27773, 27820]),
        ..CraftingRange(4, "21", "53", [31117, 31118, 31119, 31120, 31121, 31122, 31123, 31124]),
        ..CraftingRange(4, "21", "53", [27693, 27714, 27714, 27715, 27742, 27757, 27777, 27843]),
        ..CraftingRange(5, "20", "60", [31758, 31759, 31760, 31761, 31762, 31763, 31764, 31765]),
        ..CraftingRange(5, "100", "300", [31991, 31996, 31996, 32000, 31991, 31994, 31997, 31997]),
        ..CraftingRange(5, "100", "300", [31995, 31999, 32000, 31995, 31993, 31998, 31992, 31998]),
        Item(1, 29671, 340, 16),
        Item(1, 29672, 120, 16),
        Item(1, 29669, 340, 17),
        Item(1, 29670, 120, 17),
        Item(1, 29673, 40, 18),
        Item(2, 29676, 510, 16),
        Item(2, 29677, 180, 16),
        Item(2, 29674, 510, 17),
        Item(2, 29675, 180, 17),
        Item(2, 29678, 60, 18),
        Item(3, 31127, 500, 16),
        Item(3, 31128, 180, 16),
        Item(3, 31125, 500, 17),
        Item(3, 31126, 180, 17),
        Item(3, 31129, 60, 18),
        Item(4, 31132, 600, 16),
        Item(4, 31133, 200, 16),
        Item(4, 31130, 600, 17),
        Item(4, 31131, 200, 17),
        Item(4, 31134, 70, 18),
        Range(5, 31768, "36", "250", 16),
        Item(5, 31769, 750, 16),
        Range(5, 31766, "36", "250", 17),
        Item(5, 31767, 750, 17),
        Range(5, 31770, "50/200", string.Empty, 18),
        Range(5, 31771, "50/200", string.Empty, 18)
    ];

    internal static readonly RelicMaterialRequirement[] Splendorous =
    [
        ..CraftingRange(1, "20", "60", [38748, 38749, 38750, 38751, 38752, 38753, 38754, 38755]),
        ..CraftingRange(1, "40", "120", [36193, 36176, 36193, 36164, 36203, 36203, 36260, 36094]),
        ..CraftingRange(2, "30", "90", [38748, 38749, 38750, 38751, 38752, 38753, 38754, 38755]),
        ..CraftingRange(2, "30/60", "90/180", [36200, 36186, 36200, 36186, 36200, 36212, 36229, 36077]),
        ..CraftingRange(2, "30/60", "90/180", [36172, 36172, 36172, 36172, 36212, 36251, 36231, 36080]),
        ..CraftingRange(3, "30", "90", [39765, 39766, 39767, 39768, 39769, 39770, 39771, 39772]),
        ..CraftingRange(3, "30/90", "90/270", [36198, 36183, 36171, 36183, 36250, 36171, 36230, 36078]),
        ..CraftingRange(3, "30/90", "90/270", [7026, 19943, 12610, 27710, 27748, 7026, 27778, 27841]),
        ..CraftingRange(4, "30", "90", [39765, 39766, 39767, 39768, 39769, 39770, 39771, 39772]),
        ..CraftingRange(4, "30/90", "90/270", [36200, 36172, 36172, 36186, 36251, 36212, 36199, 36079]),
        ..CraftingRange(4, "30/90", "90/270", [36199, 36171, 36171, 36184, 36184, 36211, 36229, 36081]),
        ..CraftingRange(4, "30/60", "90/180", [36171, 36184, 36193, 36171, 5348, 36249, 27778, 12908]),
        ..CraftingRange(5, "20", "60", [41246, 41247, 41248, 41249, 41250, 41251, 41252, 41253]),
        ..CraftingRange(5, "20", "60", [5230, 36178, 36190, 36175, 36190, 19979, 36257, 36256]),
        ..CraftingRange(5, "20", "60", [36194, 36166, 27697, 36164, 36165, 36206, 36258, 27832]),
        ..CraftingRange(6, "20", "60", [41246, 41247, 41248, 41249, 41250, 41251, 41252, 41253]),
        ..CraftingRange(6, "20", "60", [36263, 36175, 36165, 36263, 36203, 36203, 36264, 36085]),
        ..CraftingRange(6, "20", "60", [5231, 36194, 5391, 36181, 19979, 36205, 36260, 4836]),
        ..CraftingRange(6, "20", "60", [7008, 5360, 36164, 7008, 36206, 36206, 36261, 27825]),
        Range(1, 38790, "60", "180", 16),
        Item(1, 38791, 180, 16),
        Range(1, 38788, "60", "180", 17),
        Item(1, 38789, 180, 17),
        Range(1, 38792, "30/60", string.Empty, 18),
        Range(1, 38793, "30/60", string.Empty, 18),
        Range(2, 38796, "70", "210", 16),
        Item(2, 38797, 210, 16),
        Range(2, 38794, "70", "210", 17),
        Item(2, 38795, 210, 17),
        Range(2, 38798, "40/80", string.Empty, 18),
        Range(2, 38799, "40/80", string.Empty, 18),
        Range(3, 39805, "70", "210", 16),
        Item(3, 39806, 210, 16),
        Range(3, 39807, "70", "210", 17),
        Item(3, 39808, 210, 17),
        Range(3, 39809, "40/80", string.Empty, 18),
        Range(3, 39810, "40/80", string.Empty, 18),
        Range(4, 39811, "70", "210", 16),
        Item(4, 39812, 210, 16),
        Range(4, 39813, "70", "210", 17),
        Item(4, 39814, 210, 17),
        Range(4, 39815, "40/80", string.Empty, 18),
        Range(4, 39816, "40/80", string.Empty, 18),
        Range(5, 41286, "74", "220", 16),
        Item(5, 41287, 220, 16),
        Range(5, 41288, "74", "220", 17),
        Item(5, 41289, 220, 17),
        Range(5, 41298, "Collection.Relic.Material.Quantity.SpiritSand170", string.Empty, 18),
        Range(5, 41299, string.Empty, string.Empty, 18),
        Range(6, 41290, "74", "220", 16),
        Item(6, 41291, 220, 16),
        Range(6, 41292, "74", "220", 17),
        Item(6, 41293, 220, 17),
        Range(6, 41300, "Collection.Relic.Material.Quantity.EssentialOil170", string.Empty, 18),
        Range(6, 41301, string.Empty, string.Empty, 18)
    ];

    private static RelicMaterialRequirement Item(int stageIndex, uint itemID, int quantity, uint jobID = 0) =>
        new(stageIndex, itemID, quantity, string.Empty, string.Empty, jobID,
            RelicMaterialScope.PerWeapon, string.Empty, 0);

    private static RelicMaterialRequirement FreeItem(
        int stageIndex,
        uint itemID,
        int quantity,
        int freeWeapons) =>
        new(stageIndex, itemID, quantity, string.Empty, string.Empty, 0,
            RelicMaterialScope.PerWeapon, string.Empty, freeWeapons);

    private static RelicMaterialRequirement Once(int stageIndex, uint itemID, int quantity) =>
        new(stageIndex, itemID, quantity, string.Empty, string.Empty, 0,
            RelicMaterialScope.OncePerSeries, string.Empty, 0);

    private static RelicMaterialRequirement Once(int stageIndex, uint itemID, string quantityText) =>
        new(stageIndex, itemID, 0, quantityText, string.Empty, 0,
            RelicMaterialScope.OncePerSeries, string.Empty, 0);

    private static RelicMaterialRequirement Cost(
        int stageIndex,
        uint itemID,
        int quantity) =>
        new(stageIndex, itemID, quantity, string.Empty, string.Empty, 0,
            RelicMaterialScope.PerWeapon, string.Empty, 0)
        {
            CostPerTargetItem = 1
        };

    private static RelicMaterialRequirement Cost(
        int stageIndex,
        uint itemID,
        int quantity,
        uint targetItemID,
        int costPerTargetItem,
        RelicMaterialScope scope = RelicMaterialScope.PerWeapon) =>
        new(stageIndex, itemID, quantity, string.Empty, string.Empty, 0, scope, string.Empty, 0)
        {
            CostTargetItemID = targetItemID,
            CostPerTargetItem = costPerTargetItem
        };

    private static RelicMaterialRequirement Progress(
        int stageIndex,
        string labelKey,
        string quantityText,
        RelicMaterialScope scope = RelicMaterialScope.PerWeapon) =>
        new(stageIndex, 0, 0, quantityText, string.Empty, 0, scope, labelKey, 0);

    private static RelicMaterialRequirement Range(
        int stageIndex,
        uint itemID,
        string quantityText,
        string maximumQuantityText,
        uint jobID) =>
        new(stageIndex, itemID, 0, quantityText, maximumQuantityText, jobID,
            RelicMaterialScope.PerWeapon, string.Empty, 0);

    private static RelicMaterialRequirement[] Crafting(int stageIndex, int quantity, uint[] itemIDs)
    {
        var result = new RelicMaterialRequirement[itemIDs.Length];
        for (var index = 0; index < itemIDs.Length; index++)
        {
            result[index] = Item(stageIndex, itemIDs[index], quantity, (uint)(index + 8));
        }

        return result;
    }

    private static RelicMaterialRequirement[] CraftingRange(
        int stageIndex,
        string quantityText,
        string maximumQuantityText,
        uint[] itemIDs)
    {
        var result = new RelicMaterialRequirement[itemIDs.Length];
        for (var index = 0; index < itemIDs.Length; index++)
        {
            result[index] = Range(stageIndex, itemIDs[index], quantityText, maximumQuantityText, (uint)(index + 8));
        }

        return result;
    }
}
