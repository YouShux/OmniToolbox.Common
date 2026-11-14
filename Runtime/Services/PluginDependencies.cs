using Dalamud.Plugin;
using OmenTools.Dalamud;

namespace OmniToolbox.Host;

internal static class PluginDependencies
{
    internal static bool IsOriginalVnavmeshEnabled(IExposedPlugin plugin) =>
        plugin.IsLoaded &&
        string.Equals(plugin.InternalName, vnavmeshIPC.INTERNAL_NAME, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(plugin.Name, "vnavmesh CN", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(plugin.Manifest.Author, "veyn, AtmoOmen", StringComparison.OrdinalIgnoreCase);

    internal static bool IsOriginalBossModEnabled(IEnumerable<IExposedPlugin> plugins) =>
        plugins.Any(static plugin => plugin.IsLoaded &&
            string.Equals(plugin.InternalName, BossModIPC.INTERNAL_NAME, StringComparison.OrdinalIgnoreCase));

    internal static bool IsBossModRebornEnabled()
    {
        var plugins = DalamudServices.PluginInterface.InstalledPlugins;
        return !IsOriginalBossModEnabled(plugins) &&
               plugins.Any(static plugin => plugin.IsLoaded &&
                   string.Equals(plugin.InternalName, BossModIPC.REBORN_INTERNAL_NAME, StringComparison.OrdinalIgnoreCase));
    }
}
