using System.Runtime.CompilerServices;
using System.Threading;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Utility;
using OmenTools.Dalamud;
using OmniToolbox.Host;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;

namespace OmniToolbox.UI.Controls;

internal static class PluginRequirementsTable
{
    private static readonly ConditionalWeakTable<Entry[], DisplayTable> Displays = new();
    private static IExposedPlugin[] Plugins = [];
    private static long Revision;
    private static long SnapshotRevision = -1;

    internal static void Initialize() => DalamudServices.PluginInterface.ActivePluginsChanged += OnPluginsChanged;

    internal static void Shutdown()
    {
        DalamudServices.PluginInterface.ActivePluginsChanged -= OnPluginsChanged;
        Plugins = [];
        Displays.Clear();
        SnapshotRevision = -1;
    }

    private static void OnPluginsChanged(IActivePluginsChangedEventArgs args) => Interlocked.Increment(ref Revision);

    private static DisplayTable GetDisplay(Entry[] entries)
    {
        var revision = Interlocked.Read(ref Revision);
        if (SnapshotRevision != revision)
        {
            Plugins = DalamudServices.PluginInterface.InstalledPlugins.ToArray();
            SnapshotRevision = revision;
        }
        var display = Displays.GetValue(entries, static _ => new());
        if (display.Revision == revision && display.LocalizationRevision == OmniLoc.Revision)
            return display;
        display.Revision = revision;
        display.LocalizationRevision = OmniLoc.Revision;
        display.Rows = new DisplayRow[entries.Length];
        display.ViewRows = new PluginRequirementsView.Row[entries.Length];
        var originalBossModEnabled = PluginDependencies.IsOriginalBossModEnabled(Plugins);
        PluginDependencies.GetLoadedRotation(Plugins, out var rotationConflict);
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var plugin = FindPlugin(entry, Plugins);
            var originalVnavmesh = entry == Vnavmesh && plugin is not null &&
                                   PluginDependencies.IsOriginalVnavmeshEnabled(plugin);
            var bossModBlocked = entry == BossModReborn && originalBossModEnabled;
            var rotationBlocked = rotationConflict && plugin is { IsLoaded: true } &&
                                  PluginDependencies.GetRotationProvider(plugin.InternalName) != null;
            var blocked = bossModBlocked || rotationBlocked;
            var reason = OmniLoc.Get(entry.ReasonKey);
            if (originalVnavmesh)
                reason += "\n" + OmniLoc.Get("Requirements.Warning.OriginalVnavmesh");
            if (bossModBlocked)
                reason += "\n" + OmniLoc.Get("Requirements.Warning.OriginalBossMod");
            if (rotationBlocked)
                reason += "\n" + OmniLoc.Get(PluginDependencies.ROTATION_CONFLICT_KEY);
            var status = "Requirements.Status.InstalledDisabled";
            if (blocked) status = "Requirements.Status.Blocked";
            else if (originalVnavmesh) status = "Requirements.Status.OriginalEnabled";
            else if (plugin is null) status = "Requirements.Status.NotInstalled";
            else if (plugin.IsLoaded) status = "Requirements.Status.Enabled";
            var name = OmniLoc.Get(entry.NameKey);
            var row = new PluginRequirementsView.Row(
                entry.NameKey, name,
                plugin is null ? string.Empty : string.Format(OmniLoc.Get("Requirements.Version"), plugin.Version),
                OmniLoc.Get(status), default, reason,
                Details: entry.DetailsKey is null ? null : OmniLoc.Get(entry.DetailsKey),
                DrawIcon: size => DrawPluginIcon(plugin, size),
                Open: () =>
                {
                    var url = entry.URL ?? plugin?.Manifest.RepoUrl;
                    if (!string.IsNullOrWhiteSpace(url)) Util.OpenLink(url);
                    else DalamudServices.PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.AllPlugins, name);
                },
                OpenTooltip: OmniLoc.Get(entry.URL is null ? "Requirements.OpenPlugin" : "Requirements.OpenGitHub"));
            display.Rows[index] = new(row, plugin, blocked, originalVnavmesh);
        }
        return display;
    }

    private static PluginRequirementsView.Row[] GetRows(Entry[] entries, bool conflict = false)
    {
        var display = GetDisplay(entries);
        var tokens = OmniTheme.Tokens;
        for (var index = 0; index < display.Rows.Length; index++)
        {
            var row = display.Rows[index];
            var color = conflict ? tokens.Text : tokens.Warning;
            if (row.Blocked) color = tokens.Error;
            else if (row.OriginalVnavmesh) color = tokens.Warning;
            else if (row.Plugin is null) color = conflict ? tokens.Text : tokens.Error;
            else if (row.Plugin.IsLoaded) color = conflict ? tokens.Error : tokens.Success;
            display.ViewRows[index] = row.View with { StatusColor = color };
        }
        return display.ViewRows;
    }

    internal static void DrawCollapsible(string tableID, Entry[] entries, Action? drawStatus = null,
        int maximumVisibleRows = MAX_VISIBLE_ROWS) =>
        PluginRequirementsView.DrawCollapsible(tableID, GetRows(entries),
            maximumVisibleRows: maximumVisibleRows, drawStatus: drawStatus);

    internal static void Draw(
        string? titleKey,
        string tableID,
        Entry[] entries,
        bool conflict = false,
        float minimumPluginWidth = 0f,
        bool limitVisibleRows = false,
        int maximumVisibleRows = MAX_VISIBLE_ROWS)
    {
        if (titleKey is not null)
        {
            OmniControls.SectionLabel(OmniLoc.Get(titleKey));
            ImGui.Spacing();
        }
        PluginRequirementsView.Draw(tableID, GetRows(entries, conflict),
            limitVisibleRows: limitVisibleRows, maximumVisibleRows: maximumVisibleRows,
            minimumPluginWidth: minimumPluginWidth);
    }

    internal static float MeasurePluginInfoWidth(Entry[] entries) =>
        PluginRequirementsView.MeasurePluginInfoWidth(GetRows(entries));

    private static void DrawPluginIcon(IExposedPlugin? plugin, Vector2 size)
    {
        var cursor = ImGui.GetCursorScreenPos();
        var position = new Vector2(MathF.Round(cursor.X), MathF.Round(cursor.Y));
        if (PluginIconResolver.GetIcon(plugin, size.X) is { } texture)
            ImGui.GetWindowDrawList().AddImage(texture.Handle, position, position + size);
        ImGui.Dummy(size);
    }

    private static IExposedPlugin? FindPlugin(Entry entry, IEnumerable<IExposedPlugin> installedPlugins)
    {
        IExposedPlugin? match = null;
        foreach (var plugin in installedPlugins)
        {
            if (entry == AutoDutyCN && !PluginDependencies.IsAutoDutyCN(plugin)) continue;
            foreach (var identifier in entry.Identifiers)
            {
                if (string.Equals(plugin.InternalName, identifier, StringComparison.OrdinalIgnoreCase) ||
                    (!entry.MatchInternalNameOnly && string.Equals(plugin.Name, identifier, StringComparison.OrdinalIgnoreCase)))
                {
                    if (plugin.IsLoaded) return plugin;
                    match ??= plugin;
                    break;
                }
            }
        }
        return match;
    }

    internal readonly record struct Entry(
        string NameKey, string ReasonKey, string? DetailsKey, string? URL, string[] Identifiers,
        bool MatchInternalNameOnly = false);

    private sealed class DisplayTable
    {
        internal long Revision = -1;
        internal long LocalizationRevision = -1;
        internal DisplayRow[] Rows = [];
        internal PluginRequirementsView.Row[] ViewRows = [];
    }

    private readonly record struct DisplayRow(
        PluginRequirementsView.Row View, IExposedPlugin? Plugin, bool Blocked, bool OriginalVnavmesh);

    internal static readonly Entry Vnavmesh = new("Requirements.Plugin.Vnavmesh", "Requirements.Reason.Vnavmesh", null,
        "https://github.com/AtmoOmen/ffxiv_navmesh-cn", [vnavmeshIPC.INTERNAL_NAME], MatchInternalNameOnly: true);
    private static readonly Entry BossModReborn = new("Requirements.Plugin.BossMod", "Requirements.Reason.BossMod", null,
        "https://github.com/44451516-ff14/BossmodRebornCN", [BossModIPC.REBORN_INTERNAL_NAME], MatchInternalNameOnly: true);
    private static readonly Entry RotationSolverReborn = new("Requirements.Plugin.RotationSolverReborn",
        "Requirements.Reason.Rotation", null, null, [PluginDependencies.RSR_PLUGIN], MatchInternalNameOnly: true);
    internal static readonly Entry[] CombatAndNavigationPlugins =
    [
        new("Requirements.Plugin.AEAssist", "Requirements.Reason.Rotation", null, null, [PluginDependencies.AE_ASSIST_PLUGIN]),
        new("Requirements.Plugin.PromeRotation", "Requirements.Reason.Rotation", null, null, [PluginDependencies.PROME_ROTATION_PLUGIN]),
        RotationSolverReborn,
        BossModReborn,
        Vnavmesh
    ];
    private static readonly Entry AutoDutyCN = new("Requirements.Plugin.AutoDuty", "Requirements.Reason.AutoDuty", null,
        "https://github.com/YouShux/AutoDuty", ["AutoDuty"], MatchInternalNameOnly: true);
    internal static readonly Entry[] DutyCombatAndNavigationPlugins = [AutoDutyCN, ..CombatAndNavigationPlugins];
    private const int MAX_VISIBLE_ROWS = 5;
}
