using OmniToolbox.Common.Module.Enums;
using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Cloud;

public sealed record PublicTestModule
(
    string FeatureID,
    string ModuleName,
    string TitleKey,
    string DescriptionKey,
    ModuleCategory Category,
    int MinimumLevel
)
{
    public string Author { get; init; } = "YouShu";

    public bool HasDetails { get; init; } = true;

    public string Title => OmniLoc.Get(TitleKey);

    public string Description => OmniLoc.Get(DescriptionKey);
}

public static class PublicTestModuleCatalog
{
    public static IReadOnlyList<PublicTestModule> Modules { get; } = Array.AsReadOnly<PublicTestModule>(
    [
        new("testing.craft-gather-fishing-no-animation", "CraftGatherFishingNoAnimation",
            "CraftGatherFishingNoAnimationTitle", "CraftGatherFishingNoAnimationDescription", ModuleCategory.Daily, 1),
        new("testing.gold-saucer", "AutoGoldSaucer",
            "AutoGoldSaucerTitle", "AutoGoldSaucerDescription", ModuleCategory.Automation, 2),
        new("testing.treasure-map", "TreasureMap",
            "TreasureMapTitle", "TreasureMapDescription", ModuleCategory.Automation, 3),
        new("testing.infinite-return", "InfiniteReturn",
            "InfiniteReturnTitle", "InfiniteReturnDescription", ModuleCategory.Combat, 1),
        new("testing.instant-revive", "InstantRevive",
            "InstantReviveTitle", "InstantReviveDescription", ModuleCategory.Combat, 1),
        new("testing.infinite-sprint", "InfiniteSprint",
            "InfiniteSprintTitle", "InfiniteSprintDescription", ModuleCategory.Combat, 1),
        new("testing.invulnerable-mode", "InvulnerableMode",
            "Feature.InvulnerableMode.Title", "Feature.InvulnerableMode.Description", ModuleCategory.Combat, 1),
        new("testing.instant-teleport", "InstantTeleport",
            "Feature.InstantTeleport.Title", "Feature.InstantTeleport.Description", ModuleCategory.Daily, 1),
        new("testing.ignore-flight-restriction", "IgnoreFlightRestriction",
            "Feature.IgnoreFlightRestriction.Title", "Feature.IgnoreFlightRestriction.Description", ModuleCategory.Daily, 1),
        new("testing.ignore-mount-restriction", "IgnoreMountRestriction",
            "Feature.IgnoreMountRestriction.Title", "Feature.IgnoreMountRestriction.Description", ModuleCategory.Daily, 1),
        new("testing.prohibit-swimming", "ProhibitSwimming",
            "Feature.ProhibitSwimming.Title", "Feature.ProhibitSwimming.Description", ModuleCategory.Daily, 1)
            { HasDetails = false },
        new("testing.free-company-action", "FreeCompanyAction",
            "FreeCompanyActionTitle", "FreeCompanyActionDescription", ModuleCategory.Automation, 1),
        new("testing.cosmic-lottery", "CosmicLottery",
            "CosmicLotteryTitle", "CosmicLotteryDescription", ModuleCategory.Automation, 1),
        new("testing.desynthesis-no-animation", "DesynthesisNoAnimation",
            "Feature.DesynthesisNoAnimation.Title", "Feature.DesynthesisNoAnimation.Description", ModuleCategory.Item, 1)
    ]);
}
