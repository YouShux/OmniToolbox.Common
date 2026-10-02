using System.Globalization;
using System.IO;
using System.Linq;
using OmenTools.OmenService;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public static class OmniFontSelector
{
    private static object? InstalledSnapshot;
    private static KeyValuePair<string, string>[] SortedFonts = [];
    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true);

    public static string GetPreviewText(string path, string defaultLabel, string defaultPath)
    {
        foreach (var font in OmniFonts.GameFonts)
        {
            if (font.Key == path)
                return font.Value;
        }

        return string.IsNullOrWhiteSpace(path) || string.Equals(path, defaultPath, StringComparison.OrdinalIgnoreCase)
            ? defaultLabel
            : FontManager.Instance().InstalledFonts.GetValueOrDefault(path, Path.GetFileNameWithoutExtension(path));
    }

    public static bool Draw(string id, ref string path, ref string search, string defaultLabel, string defaultPath) =>
        Draw(id, ref path, ref search, defaultLabel, defaultPath, ImGui.GetContentRegionAvail().X);

    public static bool Draw(string id, ref string path, ref string search, string defaultLabel, string defaultPath, float width)
    {
        var installed = FontManager.Instance().InstalledFonts;
        if (!ReferenceEquals(InstalledSnapshot, installed))
        {
            SortedFonts = installed.Concat(OmniFonts.GameFonts)
                .OrderBy(font => font.Value.Any(c => c is >= '\u3400' and <= '\u9fff') ? 0 : 1)
                .ThenBy(font => font.Value, NameComparer)
                .ThenBy(font => font.Key, StringComparer.Ordinal)
                .ToArray();
            InstalledSnapshot = installed;
        }
        var isDefault = string.IsNullOrWhiteSpace(path) || string.Equals(path, defaultPath, StringComparison.OrdinalIgnoreCase);
        var label = GetPreviewText(path, defaultLabel, defaultPath);
        var maximum = OmniTheme.ClampWindowSize(ImGui.GetMainViewport().WorkSize - ImGui.GetStyle().WindowPadding * 2f);
        var height = MathF.Min(OmniTheme.Scale(360f), maximum.Y);
        ImGui.SetNextWindowSizeConstraints(new Vector2(0f, height), new Vector2(maximum.X, height));
        if (!OmniControls.BeginCombo(id, label, width))
        {
            return false;
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
                    path = defaultPath;
                    changed = true;
                }
                foreach (var font in SortedFonts)
                {
                    if (font.Key == defaultPath || font.Value == defaultLabel ||
                        (!string.IsNullOrWhiteSpace(search) &&
                         !font.Key.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                         !font.Value.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    if (OmniControls.WrappedSelectable($"{font.Value}##{font.Key}", path == font.Key))
                    {
                        path = font.Key;
                        changed = true;
                    }
                    OmniControls.HelpTooltip(font.Key);
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
}
