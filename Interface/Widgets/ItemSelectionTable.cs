using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public readonly record struct ItemSelectionTableRow(uint ItemID, bool Enabled, bool CanDelete);

public enum ItemSelectionTableAction
{
    None,
    SetAll,
    SetItem,
    Delete
}

public readonly record struct ItemSelectionTableChange(
    ItemSelectionTableAction Action,
    uint ItemID = 0,
    bool Enabled = false);

public static class ItemSelectionTable
{
    public static ItemSelectionTableChange Draw(
        string id,
        IReadOnlyList<ItemSelectionTableRow> rows,
        bool showEnabledColumn = true)
    {
        var iconSize = OmniTheme.TableItemIconSize();
        var deleteButtonSize = OmniControls.CompactButtonSize(OmniLoc.Get("Common.Delete"));
        var rowContentHeight = MathF.Max(iconSize, MathF.Max(OmniTheme.CheckboxSize(), deleteButtonSize.Y));
        var cellPadding = ImGui.GetStyle().CellPadding.Y * 2f;
        var rowHeight = rowContentHeight + cellPadding;
        var tableHeight = OmniTheme.SmallButtonSize().Y + cellPadding +
                          rowHeight * 5 + OmniTheme.BorderThickness() * 2f;

        ImGui.PushID(id);
        try
        {
            using var table = ImRaii.Table(
                "##itemSelectionTable",
                showEnabledColumn ? 3 : 2,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings,
                new Vector2(ImGui.GetContentRegionAvail().X, tableHeight));
            if (!table)
            {
                return default;
            }

            if (showEnabledColumn)
            {
                ImGui.TableSetupColumn(
                    "##enabled",
                    ImGuiTableColumnFlags.WidthFixed,
                    OmniTheme.CheckboxSize() + ImGui.GetStyle().CellPadding.X * 2f);
            }

            ImGui.TableSetupColumn(OmniLoc.Get("Common.Item"), ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(
                OmniLoc.Get("Common.Action"),
                ImGuiTableColumnFlags.WidthFixed,
                deleteButtonSize.X + ImGui.GetStyle().CellPadding.X * 2f);
            ImGui.TableSetupScrollFreeze(0, 1);

            var change = DrawHeader(rows, showEnabledColumn);
            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(rows.Count, rowHeight);
            while (clipper.Step())
            {
                for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, rowContentHeight);
                    var rowChange = DrawRow(rows[index], rowContentHeight, iconSize, deleteButtonSize, showEnabledColumn);
                    if (rowChange.Action != ItemSelectionTableAction.None)
                    {
                        change = rowChange;
                    }
                }
            }

            clipper.End();
            clipper.Destroy();
            return change;
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private static ItemSelectionTableChange DrawHeader(
        IReadOnlyList<ItemSelectionTableRow> rows,
        bool showEnabledColumn)
    {
        OmniControls.BeginTableHeaderRow();
        if (showEnabledColumn)
        {
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));
            var allEnabled = rows.Count > 0;
            for (var index = 0; index < rows.Count && allEnabled; index++)
            {
                allEnabled = rows[index].Enabled;
            }

            OmniControls.CenterTableItem(new Vector2(OmniTheme.CheckboxSize()), OmniTheme.SmallButtonSize().Y);
            using (ImRaii.Disabled(rows.Count == 0))
            {
                if (OmniControls.Checkbox("##toggleAll", ref allEnabled))
                {
                    return new(ItemSelectionTableAction.SetAll, Enabled: allEnabled);
                }
            }
        }

        OmniControls.TableHeader(OmniLoc.Get("Common.Item"));
        OmniControls.TableHeader(OmniLoc.Get("Common.Action"));
        return default;
    }

    private static ItemSelectionTableChange DrawRow(
        ItemSelectionTableRow row,
        float rowContentHeight,
        float iconSize,
        Vector2 deleteButtonSize,
        bool showEnabledColumn)
    {
        ImGui.PushID((int)row.ItemID);
        var change = default(ItemSelectionTableChange);
        if (showEnabledColumn)
        {
            ImGui.TableNextColumn();
            var enabled = row.Enabled;
            OmniControls.CenterTableItem(new Vector2(OmniTheme.CheckboxSize()), rowContentHeight);
            if (OmniControls.Checkbox("##enabled", ref enabled))
            {
                change = new(ItemSelectionTableAction.SetItem, row.ItemID, enabled);
            }
        }

        ImGui.TableNextColumn();
        DrawItemCell(row.ItemID, rowContentHeight, iconSize);

        ImGui.TableNextColumn();
        if (row.CanDelete)
        {
            OmniControls.CenterTableItem(deleteButtonSize, rowContentHeight);
            if (OmniControls.SmallButton(OmniLoc.Get("Common.Delete"), false, deleteButtonSize))
            {
                change = new(ItemSelectionTableAction.Delete, row.ItemID);
            }
        }
        else
        {
            OmniControls.TableTextCentered("-", rowContentHeight);
        }

        ImGui.PopID();
        return change;
    }

    public static void DrawItemCell(
        uint itemID,
        float rowContentHeight,
        float iconSize,
        string? displayName = null,
        bool showItemIDTooltip = true,
        bool isCollected = false,
        bool wrapName = false)
    {
        var itemName = string.IsNullOrWhiteSpace(displayName) ? LuminaWrapper.GetItemName(itemID) : displayName;
        if (string.IsNullOrWhiteSpace(itemName))
        {
            itemName = itemID.ToString();
        }

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var cellPosition = ImGui.GetCursorScreenPos();
        var iconPosition = new Vector2(
            cellPosition.X,
            cellPosition.Y + MathF.Max(0f, (rowContentHeight - iconSize) * 0.5f));
        ImGui.SetCursorScreenPos(iconPosition);
        if (LuminaGetter.TryGetRow<Item>(itemID, out var item))
        {
            FramedGameIcon.DrawItem(item, new Vector2(iconSize));
            if (isCollected)
            {
                FramedGameIcon.DrawCollectedCheck(
                    ImGui.GetWindowDrawList(),
                    iconPosition,
                    new Vector2(iconSize),
                    26f);
            }
        }
        else
        {
            ImGui.Dummy(new Vector2(iconSize));
        }

        var textSpacing = OmniTheme.Scale(6f);
        var wrapWidth = wrapName ? MathF.Max(1f, availableWidth - iconSize - textSpacing) : 0f;
        var textSize = ImGui.CalcTextSize(itemName, false, wrapWidth);
        var textPosition = new Vector2(
            cellPosition.X + iconSize + textSpacing,
            cellPosition.Y + MathF.Max(0f, (rowContentHeight - textSize.Y) * 0.5f));
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), textPosition,
            ImGui.GetColorU32(ImGuiCol.Text), itemName, wrapWidth);
        ImGui.SetCursorScreenPos(textPosition);
        ImGui.Dummy(new Vector2(
            MathF.Max(1f, MathF.Min(textSize.X, availableWidth - iconSize - textSpacing)),
            textSize.Y));
        if (showItemIDTooltip)
        {
            OmniControls.HelpTooltip(string.Format(OmniLoc.Get("Common.ItemId"), itemID));
        }
    }
}
