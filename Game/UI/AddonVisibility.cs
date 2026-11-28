using OmenTools.Extensions;
using OmenTools.Interop.Game.Helpers;

namespace OmniToolbox.Game;

public static unsafe class AddonVisibility
{
    public static bool IsVisible(string addonName) =>
        AddonHelper.TryGetByName(addonName, out var addon) && addon->IsVisible;

    public static bool IsReady(string addonName) =>
        AddonHelper.TryGetByName(addonName, out var addon) && addon->IsAddonAndNodesReady();
}
