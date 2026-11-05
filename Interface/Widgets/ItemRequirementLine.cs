using System.Globalization;
using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Items;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;

namespace OmniToolbox.UI.Controls;

public static class ItemRequirementLine
{
    private static readonly Dictionary<(uint? Item, decimal Patch), Display> Displays = [];
    private static long LocalizationRevision = -1;
    private static string Culture = string.Empty;

    internal static void ClearDisplayCaches() => Displays.Clear();
    public static void Draw
    (
        uint?   itemID,
        decimal patch
    )
    {
        if (LocalizationRevision != OmniLoc.Revision || Culture != CultureInfo.CurrentCulture.Name)
        {
            LocalizationRevision = OmniLoc.Revision;
            Culture = CultureInfo.CurrentCulture.Name;
            Displays.Clear();
        }
        var key = (itemID, patch);
        if (!Displays.TryGetValue(key, out var display))
        {
            if (Displays.Count >= 256)
                Displays.Clear();
            display = new();
            if (itemID is { } displayID && LuminaGetter.TryGetRow<Item>(displayID, out var displayItem) &&
                displayItem.ClassJobCategory.RowId != 0 && displayItem.ClassJobCategory.IsValid)
            {
                display.Job = displayItem.ClassJobCategory.Value.Name.ExtractText();
                display.Level = string.Format(OmniLoc.Get("ItemInfo.RequiredLevel"), displayItem.LevelEquip);
            }
            display.Patch = patch > 0 ? string.Format(OmniLoc.Get("Collection.Preview.Version"), ItemPatchMap.Format(patch)) : string.Empty;
            Displays.Add(key, display);
        }
        var hasPart = false;
        var lineStart = ImGui.GetCursorPosX();
        var lineEnd = lineStart + ImGui.GetContentRegionAvail().X;
        var neutral = OmniTheme.Tokens.Text with
        {
            W = 0.62f
        };
        if (itemID is { } id &&
            LuminaGetter.TryGetRow<Item>(id, out var item) &&
            item.ClassJobCategory.RowId != 0 &&
            item.ClassJobCategory.IsValid)
        {
            var category = item.ClassJobCategory.Value;
            var player = DService.Instance().ObjectTable.LocalPlayer;
            DrawPart(
                display.Job,
                player is null ? neutral
                    : category.IsClassJobIn(player.ClassJob.RowId) ? OmniTheme.Tokens.Success : OmniTheme.Tokens.Error);
            DrawPart(
                display.Level,
                player is null ? neutral
                    : player.Level >= item.LevelEquip ? OmniTheme.Tokens.Success : OmniTheme.Tokens.Error);
        }

        if (patch > 0)
        {
            DrawPart(
                display.Patch,
                neutral);
        }

        void DrawPart
        (
            string  text,
            Vector4 color
        )
        {
            if (hasPart)
            {
                var nextX = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetScrollX() +
                            ImGui.GetStyle().ItemSpacing.X;
                if (nextX + OmniTextMeasurements.Measure(text).X <= lineEnd)
                {
                    ImGui.SameLine();
                }
            }

            ImGui.PushTextWrapPos(lineEnd);
            ImGui.TextColored(color, text);
            ImGui.PopTextWrapPos();
            hasPart = true;
        }
    }

    private sealed class Display
    {
        internal string Job = string.Empty;
        internal string Level = string.Empty;
        internal string Patch = string.Empty;
    }
}
