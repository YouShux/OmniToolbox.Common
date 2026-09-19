using System.Collections.Frozen;

namespace OmniToolbox.Collections;

public enum RelicWeaponSeriesKind
{
    Ultimate,
    Elegant,
    Dream,
    Phantom,
    Mandervillous,
    Bozja,
    Eureka,
    Anima,
    Zodiac,
    DeepDungeon,
    Primal,
    Yokai,
    Cosmos,
    Splendorous,
    Skysteel,
    Huihuang,
    Cifu,
    Huaguang,
    Liuguang,
    Lockblade,
    Garo,
    Hellhound,
    Triumph,
    Lightning,
    AdvancedAnimal
}

internal enum RelicWeaponJobOrder
{
    Legacy,
    Current,
    Life,
    Ultimate
}

internal sealed class RelicWeaponSeries
{
    public required RelicWeaponSeriesKind Kind { get; init; }

    public required string LabelKey { get; init; }

    public required string[] StageLabelKeys { get; init; }

    public required uint[] JobIds { get; init; }

    public required uint[][] StageItemIds { get; init; }

    public uint[] ShieldItemIds { get; init; } = [];

    public required RelicWeaponJobOrder JobOrder { get; init; }

    public RelicMaterialRequirement[] Materials { get; init; } = [];

    public uint[] TotemItemIds { get; init; } = [];

    public bool ExportReplicas { get; init; }
}

internal static class RelicWeaponCatalog
{
    private static readonly uint[][] ZodiacItemIDs =
    [
        [1665, 1735, 1805, 1874, 1944, 2046, 2135, 2191, 2192, 7887],
        [1675, 1746, 1816, 1885, 1955, 2052, 2140, 2213, 2214, 7888],
        [6257, 6258, 6259, 6260, 6261, 6262, 6263, 6264, 6265, 9250],
        [7824, 7825, 7826, 7827, 7828, 7829, 7830, 7831, 7832, 9251],
        [7834, 7835, 7836, 7837, 7838, 7839, 7840, 7841, 7842, 9252],
        [7863, 7864, 7865, 7866, 7867, 7868, 7869, 7870, 7871, 9253],
        [8649, 8650, 8651, 8652, 8653, 8654, 8655, 8656, 8657, 9254],
        [9491, 9492, 9493, 9494, 9495, 9496, 9497, 9498, 9499, 9501],
        [10054, 10055, 10056, 10057, 10058, 10059, 10060, 10061, 10062, 10064]
    ];

    private static readonly uint[][] AnimaItemIDs =
    [
        Range(13611, 13), Range(13597, 13), Range(13223, 13), Range(14870, 13),
        Range(15223, 13), Range(15237, 13), Range(15251, 13), Range(16050, 13)
    ];

    private static readonly uint[][] EurekaItemIDs =
    [
        Range(21942, 15), Range(21958, 15), Range(21974, 15), Range(21990, 15),
        Range(22925, 15), Range(22941, 15), Range(22957, 15), Range(24039, 15),
        Range(24055, 15), Range(24071, 15), Range(24643, 15), Range(24659, 15),
        Range(24675, 15), Range(24691, 15), Range(24707, 15)
    ];

    private static readonly uint[][] BozjaItemIDs =
    [
        Range(30228, 17), Range(30767, 17), Range(30785, 17),
        Range(32651, 17), Range(32669, 17), Range(33462, 17)
    ];

    private static readonly uint[][] MandervillousItemIDs =
    [
        Range(38400, 19), Range(39144, 19), Range(39920, 19), Range(40932, 19)
    ];

    private static readonly uint[][] PhantomItemIDs =
    [
        Range(47869, 21), Range(47006, 21), Range(50032, 21),
        Range(50978, 21), Range(51000, 21)
    ];

    private static readonly uint[][] DeepDungeonItemIDs =
    [
        [15181, 15182, 15183, 15184, 15185, 15189, 15190, 15191, 15192, 15186, 15187, 15188, 15193, 20456, 20457, 27347, 27348, 35756, 35774, 43633, 43654],
        [16152, 16153, 16154, 16155, 16156, 16160, 16161, 16162, 16163, 16157, 16158, 16159, 16164, 20458, 20459, 27349, 27350, 35757, 35775, 43634, 43655],
        [22977, 22978, 22979, 22980, 22981, 22985, 22986, 22987, 22988, 22982, 22983, 22984, 22989, 22990, 22991, 27379, 27380, 35759, 35777, 43635, 43656],
        [39184, 39185, 39186, 39187, 39188, 39192, 39193, 39194, 39195, 39189, 39190, 39191, 39196, 39197, 39198, 39199, 39200, 39202, 39201, 43636, 43657],
        [39204, 39205, 39206, 39207, 39208, 39212, 39213, 39214, 39215, 39209, 39210, 39211, 39216, 39217, 39218, 39219, 39220, 39222, 39221, 43637, 43658],
        Range(47028, 21),
        Range(47050, 21)
    ];

    private static readonly uint[][] PrimalItemIDs =
    [
        [8355, 8356, 8357, 8358, 8359, 8360, 15915, 8362, 8363, 9234, 10408, 10470, 10532, 20358, 20359, 0, 0, 0, 0, 0, 0],
        [0, 8373, 8374, 8375, 8376, 8377, 15916, 8379, 8380, 9236, 10410, 10473, 10534, 20366, 20367, 0, 0, 0, 0, 0, 0],
        [8364, 8365, 8366, 8367, 8368, 15913, 8370, 8371, 8372, 9235, 10409, 10471, 10533, 20362, 20363, 0, 0, 0, 0, 0, 0],
        [8382, 8383, 8384, 8385, 8386, 15914, 8388, 8389, 8390, 9237, 10411, 10472, 10535, 20370, 20371, 0, 0, 0, 0, 0, 0],
        [7813, 7814, 7815, 7816, 7817, 14888, 7819, 7820, 7821, 9232, 10449, 10511, 10573, 20374, 20375, 0, 0, 0, 0, 0, 0],
        [15556, 15557, 15558, 15559, 15560, 15564, 15565, 15566, 15567, 15561, 15562, 15563, 15568, 20378, 20379, 0, 0, 0, 0, 0, 0],
        [9549, 9550, 9551, 9552, 9553, 9555, 9556, 9557, 9558, 9554, 10452, 10514, 10576, 20382, 20383, 0, 0, 0, 0, 0, 0],
        [15584, 15585, 15586, 15587, 15588, 15592, 15593, 15594, 15595, 15589, 15590, 15591, 15596, 20432, 20433, 0, 0, 0, 0, 0, 0],
        [15570, 15571, 15572, 15573, 15574, 15578, 15579, 15580, 15581, 15575, 15576, 15577, 15582, 20428, 20429, 0, 0, 0, 0, 0, 0],
        [15598, 15599, 15600, 15601, 15602, 15606, 15607, 15608, 15609, 15603, 15604, 15605, 15610, 20436, 20437, 37289, 37290, 0, 37291, 0, 0],
        [17604, 17605, 17606, 17607, 17608, 17612, 17613, 17614, 17615, 17609, 17610, 17611, 17616, 20440, 20441, 0, 0, 0, 0, 0, 0],
        [24320, 24321, 24322, 24323, 24324, 24328, 24329, 24330, 24331, 24325, 24326, 24327, 24332, 24333, 24334, 0, 0, 0, 0, 0, 0],
        [25022, 25023, 25024, 25025, 25026, 25030, 25031, 25032, 25033, 25027, 25028, 25029, 25034, 25035, 25036, 0, 0, 0, 0, 0, 0],
        [25038, 25039, 25040, 25041, 25042, 25046, 25047, 25048, 25049, 25043, 25044, 25045, 25050, 25051, 25052, 0, 0, 0, 0, 0, 0],
        [37317, 37318, 37319, 37320, 37321, 37325, 37326, 37327, 37328, 37322, 37323, 37324, 37329, 37330, 37331, 0, 0, 0, 0, 0, 0],
        [30068, 30069, 30070, 30071, 30072, 30076, 30077, 30078, 30079, 30073, 30074, 30075, 30080, 30081, 30082, 30083, 30084, 0, 0, 0, 0],
        [33597, 33598, 33599, 33600, 33601, 33605, 33606, 33607, 33608, 33602, 33603, 33604, 33609, 33610, 33611, 0, 0, 0, 0, 0, 0],
        [30811, 30812, 30813, 30814, 30815, 30819, 30820, 30821, 30822, 30816, 30817, 30818, 30823, 30824, 30825, 0, 0, 0, 0, 0, 0],
        [30827, 30828, 30829, 30830, 30831, 30835, 30836, 30837, 30838, 30832, 30833, 30834, 30839, 30840, 30841, 0, 0, 0, 0, 0, 0],
        [30843, 30844, 30845, 30846, 30847, 30851, 30852, 30853, 30854, 30848, 30849, 30850, 30855, 30856, 30857, 0, 0, 0, 0, 0, 0],
        [38541, 38542, 38543, 38544, 38545, 38549, 38550, 38551, 38552, 38546, 38547, 38548, 38553, 38554, 38555, 38556, 38557, 0, 0, 0, 0],
        [33887, 33888, 33889, 33890, 33891, 33895, 33896, 33897, 33898, 33892, 33893, 33894, 33899, 33900, 33901, 33902, 33903, 0, 0, 0, 0],
        [39549, 39550, 39551, 39552, 39553, 39557, 39558, 39559, 39560, 39554, 39555, 39556, 39561, 39562, 39563, 39564, 39565, 0, 0, 0, 0],
        [41600, 41601, 41602, 41603, 41604, 41608, 41609, 41610, 41611, 41605, 41606, 41607, 41612, 41613, 41614, 41615, 41616, 0, 0, 0, 0],
        [44976, 44977, 44978, 44979, 44980, 44984, 44985, 44986, 44987, 44981, 44982, 44983, 44988, 44989, 44990, 44991, 44992, 0, 0, 0, 0],
        [48173, 48174, 48175, 48176, 48177, 48181, 48182, 48183, 48184, 48178, 48179, 48180, 48185, 48186, 48187, 48188, 48189, 0, 48190, 0, 0]
    ];

    private static readonly uint[][] YokaiItemIDs =
    [
        [
            15208, 15209, 15210, 15211, 15212, 15216, 15217,
            15218, 15219, 15213, 15214, 15215, 15220, 30807,
            30808, 30809, 30810, 0, 0, 0, 0
        ]
    ];

    private static readonly uint[][] SkysteelItemIDs =
    [
        SkysteelRange(29612, 11), SkysteelRange(29623, 11), SkysteelRange(29634, 13),
        SkysteelRange(30282, 13), SkysteelRange(30293, 13), SkysteelRange(31714, 13)
    ];

    private static readonly uint[][] SplendorousItemIDs =
    [
        Range(38715, 11), Range(38726, 11), Range(38737, 11), Range(39732, 11),
        Range(39743, 11), Range(41180, 11), Range(41191, 11)
    ];

    private static readonly uint[][] UltimateItemIDs =
    [
        Range(20959, 15),
        Range(22868, 15),
        Range(28289, 17),
        Range(36943, 19),
        [
            39164, 39165, 39166, 39167, 39168, 39169, 39170,
            39171, 39172, 39173, 39174, 39175, 39176, 39177,
            39178, 39179, 39180, 39181, 39182, 43642, 43663
        ],
        Range(44721, 21),
        Range(52299, 21)
    ];

    private static readonly uint[][] ElegantItemIDs =
    [
        [
            41679, 41680, 41681, 41682, 41683, 41689, 41687,
            41688, 41690, 41684, 41685, 41686, 41691, 41692,
            41693, 41694, 41695, 41697, 41696, 44243, 44244, 41700
        ]
    ];

    private static readonly uint[][] DreamItemIDs =
    [
        [
            45047, 45048, 45049, 45050, 45051, 45052, 45053, 45054,
            45055, 45056, 45057, 45058, 45059, 45060, 45061, 45062,
            45063, 45064, 45065, 45066, 45067, 45069
        ]
    ];

    private static readonly uint[][] LockbladeItemIDs =
    [
        [
            47072, 47073, 47074, 47075, 47076, 47077, 47078,
            47079, 47080, 47081, 47082, 47083, 47084, 47085,
            47086, 47087, 47088, 47089, 47090, 47091, 47092
        ]
    ];

    private static readonly uint[][] GaroItemIDs =
    [
        [
            16067, 16068, 16069, 16070, 16071, 16075, 16076,
            16077, 16078, 16072, 16073, 16074, 16079, 0,
            0, 0, 0, 0, 0, 0, 0, 0
        ]
    ];

    private static readonly uint[][] HellhoundItemIDs =
    [
        [
            40456, 40457, 40458, 40459, 40460, 40464, 40465,
            40466, 40467, 40461, 40462, 40463, 40468, 40469,
            40470, 40471, 40472, 40474, 40473, 43639, 43660
        ]
    ];

    private static readonly uint[][] TriumphItemIDs =
    [
        [
            36963, 36964, 36965, 36966, 36967, 36971, 36972,
            36973, 36974, 36968, 36969, 36970, 36975, 36976,
            36977, 36978, 36979, 36981, 36980, 43640, 43661
        ]
    ];

    private static readonly uint[][] LightningItemIDs =
    [
        [
            6033, 6035, 6034, 6036, 6037, 6038, 6039, 6040,
            6040, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ]
    ];

    private static readonly uint[][] AdvancedAnimalItemIDs =
    [
        [
            9174, 9175, 9176, 9177, 9178, 9180, 9181, 9182,
            9183, 9179, 10417, 10480, 10542, 0, 0, 0, 0, 0,
            0, 0, 0, 0
        ]
    ];

    private static readonly uint[] UltimateShieldItemIDs =
        [20974, 22883, 28306, 36962, 39183, 44742, 52320];

    private static readonly uint[] ElegantShieldItemIDs = [41698];

    private static readonly uint[] DreamShieldItemIDs = [45068];

    private static readonly uint[] LockbladeShieldItemIDs = [47093];

    private static readonly uint[] GaroShieldItemIDs = [16080];

    private static readonly uint[] HellhoundShieldItemIDs = [40475];

    private static readonly uint[] TriumphShieldItemIDs = [36982];

    private static readonly uint[] AdvancedAnimalShieldItemIDs = [9173];

    private static readonly uint[] PhantomShieldItemIDs = [47890, 47027, 50053, 50999, 51021];

    private static readonly uint[] MandervillousShieldItemIDs = [38419, 39163, 39939, 40951];

    private static readonly uint[] BozjaShieldItemIDs = [30245, 30784, 30802, 32668, 32686, 33479];

    private static readonly uint[] EurekaShieldItemIDs =
    [
        21957, 21973, 21989, 22005, 22940,
        22956, 22972, 24054, 24070, 24086,
        24658, 24674, 24690, 24706, 24722
    ];

    private static readonly uint[] AnimaShieldItemIDs =
        [13624, 13610, 13236, 14883, 15236, 15250, 15264, 16063];

    private static readonly uint[] ZodiacShieldItemIDs =
        [0, 2306, 6266, 7833, 7843, 7872, 8658, 9500, 10063];

    private static readonly uint[] DeepDungeonShieldItemIDs =
        [15194, 16165, 22992, 39203, 39223, 47049, 47071];

    private static readonly uint[] PrimalShieldItemIDs =
    [
        0, 8381, 0, 0, 7822, 15569, 9548, 15597, 15583,
        15611, 17617, 24335, 25037, 25053, 37332, 30085, 33612,
        30826, 30842, 30858, 38558, 33904, 39566, 41617, 44993, 48192
    ];

    private static readonly uint[] YokaiShieldItemIDs = [15221];

    private static readonly uint[][] CosmosItemIDs =
    [
        [45679, 45680, 45681, 45682, 45683, 45684, 45685, 45686, 45687, 45688, 45689],
        [49053, 49054, 49055, 49056, 49057, 49058, 49059, 49060, 49061, 49062, 49063],
        [49148, 49149, 49150, 49151, 49152, 49153, 49154, 49155, 49156, 49157, 49158],
        [51778, 51779, 51780, 51781, 51782, 51783, 51784, 51785, 51786, 51787, 51788]
    ];

    private static readonly uint[][] HuihuangItemIDs =
    [
        [33154, 33155, 33156, 33157, 33158, 33159, 33160, 33161, 33356, 33357, 33358]
    ];

    private static readonly uint[][] CifuItemIDs =
    [
        [16958, 16959, 16960, 16961, 16962, 16963, 16964, 16965, 16966, 16967, 16968],
        [24821, 24822, 24823, 24824, 24825, 24826, 24827, 24828, 24829, 24830, 24831]
    ];

    private static readonly uint[][] LiuguangItemIDs =
    [
        [2326, 2353, 2378, 2403, 2428, 2454, 2479, 2505, 2531, 2557, 2583]
    ];

    private static readonly uint[][] HuaguangItemIDs =
    [
        Range(10132, 11)
    ];

    private static readonly FrozenDictionary<uint, int> LegacyJobIndexes = new Dictionary<uint, int>
    {
        [19] = 0, [21] = 2, [32] = 6, [37] = 15,
        [24] = 8, [28] = 11, [33] = 12, [40] = 17,
        [20] = 1, [22] = 3, [34] = 13, [39] = 18, [30] = 5, [41] = 19,
        [23] = 4, [31] = 7, [38] = 16,
        [25] = 9, [27] = 10, [35] = 14, [42] = 20
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, int> CurrentJobIndexes = new Dictionary<uint, int>
    {
        [19] = 0, [21] = 2, [32] = 10, [37] = 15,
        [24] = 5, [28] = 8, [33] = 12, [40] = 18,
        [20] = 1, [22] = 3, [34] = 13, [39] = 17, [30] = 9, [41] = 19,
        [23] = 4, [31] = 11, [38] = 16,
        [25] = 6, [27] = 7, [35] = 14, [36] = 21, [42] = 20
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, int> LifeJobIndexes = new Dictionary<uint, int>
    {
        [8] = 0, [9] = 1, [10] = 2, [11] = 3, [12] = 4, [13] = 5,
        [14] = 6, [15] = 7, [16] = 8, [17] = 9, [18] = 10
    }.ToFrozenDictionary();

    public static readonly uint[] UltimateTotemItemIds =
        [21197, 23175, 28633, 36810, 38951, 44743, 52321];

    public static readonly RelicWeaponSeries[] Series = BuildSeries();

    public static readonly FrozenDictionary<uint, uint> ReplicaItemIds = BuildReplicaItemIds();

    public static readonly FrozenSet<uint> AllKnownItemIds = BuildAllKnownItemIds();

    public static uint GetItemID(RelicWeaponSeries series, uint jobID, int stageIndex)
    {
        if ((uint)stageIndex >= (uint)series.StageItemIds.Length)
        {
            return 0;
        }

        var itemIndex = series.JobOrder switch
        {
            RelicWeaponJobOrder.Legacy => LegacyJobIndexes[jobID],
            RelicWeaponJobOrder.Current => CurrentJobIndexes[jobID],
            RelicWeaponJobOrder.Life => LifeJobIndexes[jobID],
            RelicWeaponJobOrder.Ultimate when stageIndex < 5 => LegacyJobIndexes[jobID],
            RelicWeaponJobOrder.Ultimate => CurrentJobIndexes[jobID],
            _ => -1
        };
        return (uint)itemIndex < (uint)series.StageItemIds[stageIndex].Length
            ? series.StageItemIds[stageIndex][itemIndex]
            : 0;
    }

    public static uint GetShieldItemID(RelicWeaponSeries series, int stageIndex) =>
        (uint)stageIndex < (uint)series.ShieldItemIds.Length
            ? series.ShieldItemIds[stageIndex]
            : 0;

    private static RelicWeaponSeries[] BuildSeries() =>
    [
        new()
        {
            Kind = RelicWeaponSeriesKind.Ultimate,
            LabelKey = "Collection.Relic.Series.Ultimate",
            StageLabelKeys = Keys("Ultimate", 7),
            JobIds = [19, 21, 32, 24, 28, 33, 20, 22, 30, 34, 23, 31, 25, 27, 35, 37, 38, 40, 39, 41, 42],
            StageItemIds = UltimateItemIDs,
            ShieldItemIds = UltimateShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Ultimate,
            TotemItemIds = UltimateTotemItemIds
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Elegant,
            LabelKey = "Collection.Relic.Series.Elegant",
            StageLabelKeys = Keys("Elegant", 1),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 36, 42],
            StageItemIds = ElegantItemIDs,
            ShieldItemIds = ElegantShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Dream,
            LabelKey = "Collection.Relic.Series.Dream",
            StageLabelKeys = Keys("Dream", 1),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 36, 42],
            StageItemIds = DreamItemIDs,
            ShieldItemIds = DreamShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Phantom,
            LabelKey = "Collection.Relic.Series.Phantom",
            StageLabelKeys = Keys("Phantom", 5),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 42],
            StageItemIds = PhantomItemIDs,
            ShieldItemIds = PhantomShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current,
            Materials = RelicMaterialCatalog.Phantom,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Mandervillous,
            LabelKey = "Collection.Relic.Series.Mandervillous",
            StageLabelKeys = Keys("Mandervillous", 4),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 23, 31, 38, 25, 27, 35],
            StageItemIds = MandervillousItemIDs,
            ShieldItemIds = MandervillousShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Legacy,
            Materials = RelicMaterialCatalog.Mandervillous,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Bozja,
            LabelKey = "Collection.Relic.Series.Bozja",
            StageLabelKeys = Keys("Bozja", 6),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 20, 22, 34, 30, 23, 31, 38, 25, 27, 35],
            StageItemIds = BozjaItemIDs,
            ShieldItemIds = BozjaShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Legacy,
            Materials = RelicMaterialCatalog.Bozja
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Eureka,
            LabelKey = "Collection.Relic.Series.Eureka",
            StageLabelKeys = Keys("Eureka", 15),
            JobIds = [19, 21, 32, 24, 28, 33, 20, 22, 34, 30, 23, 31, 25, 27, 35],
            StageItemIds = EurekaItemIDs,
            ShieldItemIds = EurekaShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Legacy,
            Materials = RelicMaterialCatalog.Eureka,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Anima,
            LabelKey = "Collection.Relic.Series.Anima",
            StageLabelKeys = Keys("Anima", 8),
            JobIds = [19, 21, 32, 24, 28, 33, 20, 22, 30, 23, 31, 25, 27],
            StageItemIds = AnimaItemIDs,
            ShieldItemIds = AnimaShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Legacy,
            Materials = RelicMaterialCatalog.Anima
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Zodiac,
            LabelKey = "Collection.Relic.Series.Zodiac",
            StageLabelKeys = Keys("Zodiac", 9),
            JobIds = [19, 21, 24, 28, 20, 22, 30, 23, 25, 27],
            StageItemIds = ZodiacItemIDs,
            ShieldItemIds = ZodiacShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current,
            Materials = RelicMaterialCatalog.Zodiac,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.DeepDungeon,
            LabelKey = "Collection.Relic.Series.DeepDungeon",
            StageLabelKeys = Keys("DeepDungeon", 7),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 42],
            StageItemIds = DeepDungeonItemIDs,
            ShieldItemIds = DeepDungeonShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Primal,
            LabelKey = "Collection.Relic.Series.Primal",
            StageLabelKeys = Keys("Primal", 26),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 42],
            StageItemIds = PrimalItemIDs,
            ShieldItemIds = PrimalShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Yokai,
            LabelKey = "Collection.Relic.Series.Yokai",
            StageLabelKeys = Keys("Yokai", 1),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 20, 22, 34, 30, 23, 31, 38, 25, 27, 35],
            StageItemIds = YokaiItemIDs,
            ShieldItemIds = YokaiShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Cosmos,
            LabelKey = "Collection.Relic.Series.Cosmos",
            StageLabelKeys = Keys("Cosmos", 4),
            JobIds = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
            StageItemIds = CosmosItemIDs,
            JobOrder = RelicWeaponJobOrder.Life,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Splendorous,
            LabelKey = "Collection.Relic.Series.Splendorous",
            StageLabelKeys = Keys("Splendorous", 7),
            JobIds = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
            StageItemIds = SplendorousItemIDs,
            JobOrder = RelicWeaponJobOrder.Life,
            Materials = RelicMaterialCatalog.Splendorous,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Skysteel,
            LabelKey = "Collection.Relic.Series.Skysteel",
            StageLabelKeys = Keys("Skysteel", 6),
            JobIds = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
            StageItemIds = SkysteelItemIDs,
            JobOrder = RelicWeaponJobOrder.Life,
            Materials = RelicMaterialCatalog.Skysteel,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Huihuang,
            LabelKey = "Collection.Relic.Series.Huihuang",
            StageLabelKeys = Keys("Huihuang", 1),
            JobIds = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
            StageItemIds = HuihuangItemIDs,
            JobOrder = RelicWeaponJobOrder.Life,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Cifu,
            LabelKey = "Collection.Relic.Series.Cifu",
            StageLabelKeys = Keys("Cifu", 2),
            JobIds = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
            StageItemIds = CifuItemIDs,
            JobOrder = RelicWeaponJobOrder.Life,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Huaguang,
            LabelKey = "Collection.Relic.Series.Huaguang",
            StageLabelKeys = Keys("Huaguang", 1),
            JobIds = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
            StageItemIds = HuaguangItemIDs,
            JobOrder = RelicWeaponJobOrder.Life,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Liuguang,
            LabelKey = "Collection.Relic.Series.Liuguang",
            StageLabelKeys = Keys("Liuguang", 1),
            JobIds = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
            StageItemIds = LiuguangItemIDs,
            JobOrder = RelicWeaponJobOrder.Life,
            ExportReplicas = true
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Lockblade,
            LabelKey = "Collection.Relic.Series.Lockblade",
            StageLabelKeys = Keys("Lockblade", 1),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 42],
            StageItemIds = LockbladeItemIDs,
            ShieldItemIds = LockbladeShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Garo,
            LabelKey = "Collection.Relic.Series.Garo",
            StageLabelKeys = Keys("Garo", 1),
            JobIds = [19, 21, 32, 24, 28, 33, 20, 22, 30, 23, 31, 25, 27],
            StageItemIds = GaroItemIDs,
            ShieldItemIds = GaroShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Hellhound,
            LabelKey = "Collection.Relic.Series.Hellhound",
            StageLabelKeys = Keys("Hellhound", 1),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 42],
            StageItemIds = HellhoundItemIDs,
            ShieldItemIds = HellhoundShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Triumph,
            LabelKey = "Collection.Relic.Series.Triumph",
            StageLabelKeys = Keys("Triumph", 1),
            JobIds = [19, 21, 32, 37, 24, 28, 33, 40, 20, 22, 39, 34, 30, 41, 23, 31, 38, 25, 27, 35, 42],
            StageItemIds = TriumphItemIDs,
            ShieldItemIds = TriumphShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.Lightning,
            LabelKey = "Collection.Relic.Series.Lightning",
            StageLabelKeys = Keys("Lightning", 1),
            JobIds = [19, 21, 24, 28, 20, 22, 23, 25, 27],
            StageItemIds = LightningItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        },
        new()
        {
            Kind = RelicWeaponSeriesKind.AdvancedAnimal,
            LabelKey = "Collection.Relic.Series.AdvancedAnimal",
            StageLabelKeys = Keys("AdvancedAnimal", 1),
            JobIds = [19, 21, 32, 24, 28, 33, 20, 22, 30, 23, 31, 25, 27],
            StageItemIds = AdvancedAnimalItemIDs,
            ShieldItemIds = AdvancedAnimalShieldItemIDs,
            JobOrder = RelicWeaponJobOrder.Current
        }
    ];

    private static FrozenDictionary<uint, uint> BuildReplicaItemIds()
    {
        var result = new Dictionary<uint, uint>();
        AddRangeMap(result, 38400, 39329, 19);
        AddRangeMap(result, 39144, 40323, 19);
        AddRangeMap(result, 39920, 40952, 19);
        AddRangeMap(result, 40932, 40992, 19);

        int[] zodiacReplicaJobOffsets = [0, 1, 2, 3, 4, 6, 7, 8, 9, 5];
        for (var stageIndex = 0; stageIndex < ZodiacItemIDs.Length; stageIndex++)
        {
            for (var jobIndex = 0; jobIndex < ZodiacItemIDs[stageIndex].Length; jobIndex++)
            {
                result.Add(
                    ZodiacItemIDs[stageIndex][jobIndex],
                    12116u + (uint)(zodiacReplicaJobOffsets[jobIndex] * ZodiacItemIDs.Length + stageIndex));
            }
        }

        AddRangeMap(result, 29634, 30304, 11);
        AddRangeMap(result, 30293, 31725, 11);
        AddRangeMap(result, 31714, 36795, 11);
        AddRangeMap(result, 21990, 23126, 15);
        AddRangeMap(result, 22957, 24125, 15);
        AddRangeMap(result, 24071, 24832, 15);
        AddRangeMap(result, 24675, 28307, 15);
        AddRangeMap(result, 24691, 28323, 15);
        AddRangeMap(result, 38737, 39754, 11);
        AddRangeMap(result, 39732, 41202, 11);
        AddRangeMap(result, 39743, 41213, 11);
        AddRangeMap(result, 41180, 41224, 11);
        AddRangeMap(result, 41191, 41235, 11);
        AddRangeMap(result, 47869, 49099, 21);
        AddRangeMap(result, 47006, 50060, 21);
        AddRangeMap(result, 50978, 51066, 21);
        AddRangeMap(result, 51000, 51088, 21);
        return result.ToFrozenDictionary();
    }

    private static FrozenSet<uint> BuildAllKnownItemIds()
    {
        var result = new HashSet<uint>();
        foreach (var series in Series)
        {
            foreach (var stage in series.StageItemIds)
            {
                result.UnionWith(stage);
            }

            result.UnionWith(series.ShieldItemIds);
            result.UnionWith(series.TotemItemIds);
            foreach (var material in series.Materials)
            {
                result.Add(material.ItemID);
            }
        }

        result.UnionWith(ReplicaItemIds.Values);
        result.Remove(0);
        return result.ToFrozenSet();
    }

    private static void AddRangeMap(IDictionary<uint, uint> target, uint sourceStart, uint replicaStart, int count)
    {
        for (var offset = 0; offset < count; offset++)
        {
            target.Add(sourceStart + (uint)offset, replicaStart + (uint)offset);
        }
    }

    private static uint[] SkysteelRange(uint start, int count)
    {
        var result = Range(start, count);
        (result[8], result[9]) = (result[9], result[8]);
        return result;
    }

    private static uint[] Range(uint start, int count)
    {
        var result = new uint[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = start + (uint)index;
        }

        return result;
    }

    private static string[] Keys(string series, int count)
    {
        var result = new string[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = $"Collection.Relic.Stage.{series}.{index + 1}";
        }

        return result;
    }
}
