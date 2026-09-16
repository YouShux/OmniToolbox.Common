using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools.Interop.Game.Helpers;
using OmniToolbox.Host;

namespace OmniToolbox.UI;

internal static unsafe class AddonWindowControl
{
    public static void Close(string addonName)
    {
        if (!AddonHelper.TryGetByName(addonName, out AtkUnitBase* addon) || !addon->IsVisible)
        {
            return;
        }

        try
        {
            addon->Close(true);
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(ex, "Failed to close addon {AddonName}.", addonName);
        }
    }
}
