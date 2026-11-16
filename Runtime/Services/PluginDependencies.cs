using Dalamud.Plugin;
using OmenTools.Dalamud;
using OmniToolbox.Config;
using OmniToolbox.UI;

namespace OmniToolbox.Host;

internal static class PluginDependencies
{
    internal const string AE_ASSIST_PLUGIN = "AEAssistV3";
    internal const string PROME_ROTATION_PLUGIN = "PromeRotation";
    internal const string RSR_PLUGIN = "RotationSolver";
    internal const string ROTATION_CONFLICT_KEY = "Requirements.Rotation.Conflict";

    internal static bool IsLoaded(string internalName) =>
        DalamudServices.PluginInterface.InstalledPlugins.Any(plugin => plugin.IsLoaded &&
            string.Equals(plugin.InternalName, internalName, StringComparison.OrdinalIgnoreCase));

    internal static string? GetVnavmeshFailure()
    {
        if (!vnavmeshIPC.IsPluginEnabled()) return "vnavmesh_unavailable";
        const string isReady = "vnavmesh.Nav.IsReady";
        return DalamudServices.PluginInterface.GetIpcSubscriber<bool>(isReady).HasFunction
            ? null : $"navigation_ipc_unavailable:{isReady}";
    }

    internal static string? DescribeFailure(string failure)
    {
        if (failure.Length == 0) return null;
        var separator = failure.IndexOf(':');
        var code = separator < 0 ? failure : failure[..separator];
        var key = code switch
        {
            "rotation_conflict" => ROTATION_CONFLICT_KEY,
            "bossmod_original_enabled" => "Requirements.Warning.OriginalBossMod",
            "autopath_disabled" => "Common.Status.AutoPathDisabled",
            _ => $"Requirements.Failure.{code}"
        };
        if (!OmniLoc.TryGet(key, OmniLoc.CurrentLanguage, out var message)) return null;
        return separator < 0 ? message : string.Format(message, failure[(separator + 1)..]);
    }

    internal static CombatRotationProvider? GetRotationProvider(string internalName) => internalName switch
    {
        AE_ASSIST_PLUGIN => CombatRotationProvider.AEAssist,
        PROME_ROTATION_PLUGIN => CombatRotationProvider.PromeRotation,
        RSR_PLUGIN => CombatRotationProvider.RotationSolverReborn,
        _ => null
    };

    internal static CombatRotationProvider? GetLoadedRotation(IEnumerable<IExposedPlugin> plugins, out bool conflict)
    {
        conflict = false;
        CombatRotationProvider? loaded = null;
        foreach (var plugin in plugins)
        {
            if (!plugin.IsLoaded || GetRotationProvider(plugin.InternalName) is not { } provider)
                continue;
            if (loaded is { } previous && previous != provider)
            {
                conflict = true;
                return null;
            }
            loaded = provider;
        }
        return loaded;
    }

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
