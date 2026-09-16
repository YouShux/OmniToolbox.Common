using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Items;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public static class ItemRequirementLine
{
    public static void Draw(uint? itemID, decimal patch)
    {
        var hasPart = false;
        var lineStart = ImGui.GetCursorPosX();
        var lineEnd = lineStart + ImGui.GetContentRegionAvail().X;
        var neutral = OmniTheme.Tokens.Text with { W = 0.62f };
        if (itemID is { } id &&
            LuminaGetter.TryGetRow<Item>(id, out var item) &&
            item.ClassJobCategory.RowId != 0 &&
            item.ClassJobCategory.IsValid)
        {
            var category = item.ClassJobCategory.Value;
            var player = DService.Instance().ObjectTable.LocalPlayer;
            DrawPart(
                category.Name.ExtractText(),
                player is null ? neutral
                    : category.IsClassJobIn(player.ClassJob.RowId) ? OmniTheme.Tokens.Success : OmniTheme.Tokens.Error);
            DrawPart(
                string.Format(OmniLoc.Get("ItemInfo.RequiredLevel"), item.LevelEquip),
                player is null ? neutral
                    : player.Level >= item.LevelEquip ? OmniTheme.Tokens.Success : OmniTheme.Tokens.Error);
        }

        if (patch > 0)
        {
            DrawPart(
                string.Format(OmniLoc.Get("Collection.Preview.Version"), ItemPatchMap.Format(patch)),
                neutral);
        }

        void DrawPart(string text, Vector4 color)
        {
            if (hasPart)
            {
                var nextX = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetScrollX() +
                            ImGui.GetStyle().ItemSpacing.X;
                if (nextX + ImGui.CalcTextSize(text).X <= lineEnd)
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
}
