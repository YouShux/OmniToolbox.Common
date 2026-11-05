using System.Globalization;
using System.IO;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;
using TreeToolKit;

namespace OmniToolbox.UI.Controls;

public static class OmniFontSelector
{
    private static object?                        InstalledSnapshot;
    private static KeyValuePair<string, string>[] SortedFonts       = [];
    private static readonly Dictionary<string, string> FontNames = new(StringComparer.Ordinal);
    private static readonly List<FontOption> VisibleFonts = [];
    private static bool FontsSorted;
    private static (string Search, string DefaultPath, string DefaultLabel)? VisibleQuery;
    private static (float Width, nint Font, float Size, long Revision, Vector2 Padding)? VisibleLayout;

    internal static void ClearDisplayCaches()
    {
        InstalledSnapshot = null;
        SortedFonts = [];
        FontNames.Clear();
        VisibleFonts.Clear();
        VisibleQuery = null;
        VisibleLayout = null;
        FontsSorted = false;
    }

    public static string GetPreviewText
    (
        string path,
        string defaultLabel,
        string defaultPath
    )
    {
        UpdateFonts();
        if (!string.IsNullOrWhiteSpace(path) && FontNames.TryGetValue(path, out var name))
            return name;

        return string.IsNullOrWhiteSpace(path) || string.Equals(path, defaultPath, StringComparison.OrdinalIgnoreCase)
            ? defaultLabel
            : Path.GetFileNameWithoutExtension(path);
    }

    public static bool Draw
    (
        string     id,
        ref string path,
        ref string search,
        string     defaultLabel,
        string     defaultPath
    ) =>
        Draw(id, ref path, ref search, defaultLabel, defaultPath, ImGui.GetContentRegionAvail().X);

    public static bool Draw
    (
        string     id,
        ref string path,
        ref string search,
        string     defaultLabel,
        string     defaultPath,
        float      width
    )
    {
        var isDefault = string.IsNullOrWhiteSpace(path) || string.Equals(path, defaultPath, StringComparison.OrdinalIgnoreCase);
        var label = GetPreviewText(path, defaultLabel, defaultPath);
        var maximum = OmniTheme.ClampWindowSize(OmniWindowPlacement.GetWorkArea().Size - ImGui.GetStyle().WindowPadding * 2f);
        var height = MathF.Min(OmniTheme.Scale(360f), maximum.Y);
        ImGui.SetNextWindowSizeConstraints(new Vector2(0f, height), new Vector2(maximum.X, height));
        if (!OmniControls.BeginCombo(id, label, width))
        {
            return false;
        }

        if (!FontsSorted)
        {
            SortedFonts = Fonts.GetInstalledFonts().Concat(Fonts.GameFonts)
                .OrderBy(font => font.Value.Any(c => c is >= '\u3400' and <= '\u9fff') ? 0 : 1)
                .ThenBy(font => font.Value, StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true))
                .ThenBy(font => font.Key, StringComparer.Ordinal)
                .ToArray();
            FontsSorted = true;
        }

        var changed = false;
        var searchChanged = OmniControls.InputTextWithHint("##fontSearch", OmniLoc.Get("Settings.Ui.Font.Search"),
            ref search, 128, ImGui.GetContentRegionAvail().X);
        using (var child = ImRaii.Child("##fontList", new Vector2(0f, MathF.Max(1f, ImGui.GetContentRegionAvail().Y))))
        {
            if (child)
            {
                if (searchChanged)
                {
                    ImGui.SetScrollY(0f);
                }
                if (OmniControls.WrappedSelectable(defaultLabel, isDefault))
                {
                    path    = defaultPath;
                    changed = true;
                }
                UpdateVisibleFonts(search, defaultPath, defaultLabel);
                var interaction = new OmniListInteraction("##fontRows");
                foreach (var font in VisibleFonts)
                {
                    var rowID = ImGui.GetID(font.Path);
                    if (!interaction.ShouldRetain(rowID) && !ImGui.IsRectVisible(new Vector2(ImGui.GetContentRegionAvail().X, font.Height)))
                    {
                        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, font.Height));
                        continue;
                    }
                    using var rowScope = interaction.BeginRow(rowID);
                    if (OmniControls.WrappedSelectable(font.Label, path == font.Path))
                    {
                        path    = font.Path;
                        changed = true;
                    }
                    OmniControls.HelpTooltip(font.Path);
                }
            }
        }
        if (changed)
        {
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndCombo();
        return changed;
    }

    private static void UpdateFonts()
    {
        var installed = Fonts.GetInstalledFonts();
        if (ReferenceEquals(InstalledSnapshot, installed))
            return;
        InstalledSnapshot = installed;
        FontNames.Clear();
        foreach (var font in Fonts.GameFonts)
            FontNames.TryAdd(font.Key, font.Value);
        foreach (var font in installed)
            FontNames.TryAdd(font.Key, font.Value);
        FontsSorted = false;
        VisibleQuery = null;
        VisibleLayout = null;
    }

    private static unsafe void UpdateVisibleFonts(string search, string defaultPath, string defaultLabel)
    {
        var query = (search, defaultPath, defaultLabel);
        if (VisibleQuery != query)
        {
            VisibleQuery = query;
            VisibleLayout = null;
            VisibleFonts.Clear();
            foreach (var font in SortedFonts)
            {
                if (font.Key == defaultPath || font.Value == defaultLabel ||
                    (!string.IsNullOrWhiteSpace(search) &&
                     !font.Key.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                     !font.Value.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    continue;
                VisibleFonts.Add(new(font.Key, font.Value, $"{font.Value}##{font.Key}"));
            }
        }
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var padding = ImGui.GetStyle().FramePadding;
        var layout = (width, (nint)ImGui.GetFont().Handle, ImGui.GetFontSize(), Fonts.Revision, padding);
        if (VisibleLayout == layout)
            return;
        VisibleLayout = layout;
        var textWidth = MathF.Max(1f, width - MathF.Min(padding.X, MathF.Max(0f, (width - 1f) * 0.5f)) * 2f);
        foreach (var font in VisibleFonts)
            font.Height = MathF.Ceiling(MathF.Max(OmniTextMeasurements.Measure(font.Name, false, textWidth).Y,
                ImGui.GetTextLineHeight()) + padding.Y * 2f);
    }

    private sealed class FontOption(string path, string name, string label)
    {
        public string Path { get; } = path;
        public string Name { get; } = name;
        public string Label { get; } = label;
        public float Height { get; set; }
    }
}
