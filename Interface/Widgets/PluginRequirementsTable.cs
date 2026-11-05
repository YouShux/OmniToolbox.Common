using System.Runtime.CompilerServices;
using System.Threading;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Utility;
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
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var plugin = FindPlugin(entry, Plugins);
            display.Rows[index] = new(entry, plugin, OmniLoc.Get(entry.NameKey),
                plugin is null ? string.Empty : string.Format(OmniLoc.Get("Requirements.Version"), plugin.Version),
                OmniLoc.Get(entry.ReasonKey), $"##openPluginLink{entry.NameKey}");
        }
        display.Labels = [OmniLoc.Get("Requirements.Column.Icon"), OmniLoc.Get("Requirements.Column.Plugin"),
            OmniLoc.Get("Requirements.Column.Link"), OmniLoc.Get("Requirements.Column.Status"),
            OmniLoc.Get("Requirements.Column.Reason")];
        return display;
    }

    internal static void Draw
    (
        string  titleKey,
        string  tableID,
        Entry[] entries,
        bool    conflict = false,
        float   minimumPluginWidth = 0f
    )
    {
        var display = GetDisplay(entries);
        var pluginWidth = MathF.Max(minimumPluginWidth, MeasurePluginInfoWidth(entries));
        var statusWidth = MathF.Max(OmniTextMeasurements.Measure(OmniLoc.Get("Requirements.Column.Status")).X,
            MathF.Max(OmniTextMeasurements.Measure(OmniLoc.Get("Requirements.Status.NotInstalled")).X,
                MathF.Max(OmniTextMeasurements.Measure(OmniLoc.Get("Requirements.Status.Enabled")).X,
                    OmniTextMeasurements.Measure(OmniLoc.Get("Requirements.Status.InstalledDisabled")).X)));
        OmniControls.SectionLabel(OmniLoc.Get(titleKey));
        ImGui.Spacing();

        var labels = display.Labels;
        var columnPadding = ImGui.GetStyle().CellPadding.X * 2f;
        var iconWidth = MathF.Ceiling(MathF.Max(OmniTheme.Scale(40f), OmniTextMeasurements.Measure(labels[0]).X)) + columnPadding;
        var linkWidth = MathF.Ceiling(MathF.Max(OmniTheme.CheckboxSize(), OmniTextMeasurements.Measure(labels[2]).X)) + columnPadding;
        pluginWidth = MathF.Ceiling(pluginWidth) + columnPadding;
        statusWidth = MathF.Ceiling(statusWidth) + columnPadding;
        var minimumReasonWidth = MathF.Max(OmniTextMeasurements.Measure(labels[4]).X, ImGui.GetFontSize() * 8f) + columnPadding;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var fixedWidth = iconWidth + pluginWidth + linkWidth + statusWidth + columnPadding * 4f;
        var tableWidth = MathF.Max(availableWidth, fixedWidth + minimumReasonWidth);
        var horizontalScroll = tableWidth > availableWidth;
        var reasonWidth = MathF.Max(1f, MathF.Floor(tableWidth - fixedWidth));
        Span<float> rowHeights = stackalloc float[entries.Length];
        var tableHeight = MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight()) +
                          ImGui.GetStyle().CellPadding.Y * 2f;
        for (var index = 0; index < entries.Length; index++)
        {
            var textHeight = display.Rows[index].Plugin is null
                ? ImGui.GetTextLineHeight()
                : ImGui.GetTextLineHeight() * 2f + ImGui.GetStyle().ItemSpacing.Y;
            rowHeights[index] = MathF.Ceiling(MathF.Max(
                MathF.Max(textHeight, MathF.Max(OmniTheme.Scale(40f), OmniTheme.CheckboxSize())),
                OmniTextMeasurements.Measure(display.Rows[index].Reason, false, reasonWidth).Y));
            tableHeight += rowHeights[index] + ImGui.GetStyle().CellPadding.Y * 2f;
        }
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg |
                    ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.NoSavedSettings;
        if (horizontalScroll)
            flags |= ImGuiTableFlags.ScrollX;
        using var table = ImRaii.Table(tableID, 5,
            flags,
            new Vector2(availableWidth, horizontalScroll ? MathF.Ceiling(tableHeight + ImGui.GetStyle().ScrollbarSize) : 0f),
            horizontalScroll ? tableWidth : 0f);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(labels[0], ImGuiTableColumnFlags.WidthFixed, iconWidth);
        ImGui.TableSetupColumn(labels[1], ImGuiTableColumnFlags.WidthFixed, pluginWidth);
        ImGui.TableSetupColumn(labels[2], ImGuiTableColumnFlags.WidthFixed, linkWidth);
        ImGui.TableSetupColumn(labels[3], ImGuiTableColumnFlags.WidthFixed, statusWidth);
        ImGui.TableSetupColumn(labels[4], ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(2, 0);
        OmniControls.BeginTableHeaderRow();
        OmniControls.TableHeader(labels[0]);
        OmniControls.TableHeader(labels[1]);
        OmniControls.TableHeader(labels[2]);
        OmniControls.TableHeader(labels[3]);
        OmniControls.TableHeader(labels[4]);

        for (var index = 0; index < entries.Length; index++)
        {
            DrawRow(display.Rows[index], conflict, rowHeights[index]);
        }
    }

    private static void DrawRow
    (
        DisplayRow                  display,
        bool                        conflict,
        float                       rowContentHeight
    )
    {
        var entry = display.Entry;
        var plugin = display.Plugin;
        var iconSize = new Vector2(MathF.Round(OmniTheme.Scale(40f)));
        var buttonSize = new Vector2(OmniTheme.CheckboxSize());
        var name = display.Name;
        var version = display.Version;
        var reason = display.Reason;
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        var textHeight = ImGui.GetTextLineHeight() * (plugin is null ? 1f : 2f) +
            (plugin is null ? 0f : ImGui.GetStyle().ItemSpacing.Y);
        var top = ImGui.GetCursorPosY();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - iconSize.X) * 0.5f));
        ImGui.SetCursorPosY(top + (rowContentHeight - iconSize.Y) * 0.5f);
        DrawPluginIcon(plugin, iconSize);
        ImGui.TableSetColumnIndex(1);
        ImGui.SetCursorPosY(top + (rowContentHeight - textHeight) * 0.5f);
        using (ImRaii.TextWrapPos(-1f))
        {
            ImGui.TextUnformatted(name);
            if (plugin is not null)
            {
                ImGui.TextColored(OmniTheme.Tokens.Text with
                {
                    W = 0.65f
                }, version);
            }
        }
        ImGui.TableSetColumnIndex(2);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - buttonSize.X) * 0.5f));
        ImGui.SetCursorPosY(top + (rowContentHeight - buttonSize.Y) * 0.5f);
        if (OmniControls.IconButton(
                display.LinkID,
                FontAwesomeIcon.ExternalLinkAlt,
                false,
                buttonSize,
                OmniLoc.Get(entry.URL is null ? "Requirements.OpenPlugin" : "Requirements.OpenGitHub"),
                favoriteStyle: true))
        {
            var url = entry.URL ?? plugin?.Manifest.RepoUrl;
            if (!string.IsNullOrWhiteSpace(url))
                Util.OpenLink(url);
            else
                DalamudServices.PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.AllPlugins, name);
        }

        ImGui.TableSetColumnIndex(3);
        DrawStatus(plugin, conflict, rowContentHeight);
        ImGui.TableSetColumnIndex(4);
        OmniControls.TableTextCentered(reason, rowContentHeight);
        if (entry.DetailsKey is not null)
        {
            OmniControls.HelpTooltip(OmniLoc.Get(entry.DetailsKey));
        }
    }

    private static void DrawPluginIcon
    (
        IExposedPlugin? plugin,
        Vector2         size
    )
    {
        var cursor = ImGui.GetCursorScreenPos();
        var position = new Vector2(MathF.Round(cursor.X), MathF.Round(cursor.Y));
        if (PluginIconResolver.GetIcon(plugin, size.X) is { } texture)
        {
            ImGui.GetWindowDrawList().AddImage(texture.Handle, position, position + size);
        }

        ImGui.Dummy(size);
    }

    private static void DrawStatus
    (
        IExposedPlugin? plugin,
        bool            conflict,
        float           rowHeight
    )
    {
        var tokens = OmniTheme.Tokens;
        if (plugin is null)
        {
            OmniControls.TableTextCentered(
                OmniLoc.Get("Requirements.Status.NotInstalled"),
                rowHeight,
                conflict ? tokens.Text : tokens.Error);
            return;
        }

        if (plugin.IsLoaded)
        {
            OmniControls.TableTextCentered(
                OmniLoc.Get("Requirements.Status.Enabled"),
                rowHeight,
                conflict ? tokens.Error : tokens.Success);
            return;
        }

        OmniControls.TableTextCentered(
            OmniLoc.Get("Requirements.Status.InstalledDisabled"),
            rowHeight,
            conflict ? tokens.Text : tokens.Warning);
    }

    internal static float MeasurePluginInfoWidth
    (
        Entry[] entries
    )
    {
        var width = OmniTextMeasurements.Measure(OmniLoc.Get("Requirements.Column.Plugin")).X;
        foreach (var row in GetDisplay(entries).Rows)
        {
            width = MathF.Max(width, OmniTextMeasurements.Measure(row.Name).X);
            if (row.Plugin is not null)
                width = MathF.Max(width, OmniTextMeasurements.Measure(row.Version).X);
        }
        return width;
    }

    private static IExposedPlugin? FindPlugin
    (
        Entry                       entry,
        IEnumerable<IExposedPlugin> installedPlugins
    )
    {
        IExposedPlugin? match = null;
        foreach (var plugin in installedPlugins)
        {
            foreach (var identifier in entry.Identifiers)
            {
                if (string.Equals(plugin.InternalName, identifier, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(plugin.Name, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    if (plugin.IsLoaded)
                        return plugin;
                    match ??= plugin;
                    break;
                }
            }
        }

        return match;
    }

    internal readonly record struct Entry
    (
        string   NameKey,
        string   ReasonKey,
        string?  DetailsKey,
        string?  URL,
        string[] Identifiers
    );

    private sealed class DisplayTable
    {
        internal long Revision = -1;
        internal long LocalizationRevision = -1;
        internal DisplayRow[] Rows = [];
        internal string[] Labels = [];
    }

    private readonly record struct DisplayRow(Entry Entry, IExposedPlugin? Plugin, string Name, string Version,
        string Reason, string LinkID);
}
