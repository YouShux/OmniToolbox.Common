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
        var available = ImGui.GetContentRegionAvail();
        var fixedWidth = deleteButtonSize.X + (showEnabledColumn ? OmniControls.MeasureCheckbox(string.Empty).X : 0f);
        var nameWidth = available.X - fixedWidth - ImGui.GetStyle().CellPadding.X * (showEnabledColumn ? 6f : 4f) -
                        ImGui.GetStyle().ScrollbarSize;
        var detailLayout = nameWidth < iconSize + OmniTheme.Scale(6f) + ImGui.GetFontSize() * 4f;
        if (detailLayout)
        {
            nameWidth = MathF.Max(1f, available.X - ImGui.GetStyle().CellPadding.X * 2f - ImGui.GetStyle().ScrollbarSize);
        }
        foreach (var row in rows)
        {
            rowContentHeight = MathF.Max(rowContentHeight, MeasureItemCellHeight(LuminaWrapper.GetItemName(row.ItemID), nameWidth, iconSize));
        }
        var cellPadding = ImGui.GetStyle().CellPadding.Y * 2f;
        var rowHeight = rowContentHeight + cellPadding;
        var tableHeight = OmniTheme.SmallButtonSize().Y + cellPadding +
                          rowHeight * Math.Clamp(rows.Count, 1, 5) + OmniTheme.BorderThickness() * 2f;

        ImGui.PushID(id);
        try
        {
            using var table = ImRaii.Table(
                "##itemSelectionTable",
                detailLayout ? 1 : showEnabledColumn ? 3 : 2,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings,
                new Vector2(ImGui.GetContentRegionAvail().X, tableHeight));
            if (!table)
            {
                return default;
            }

            if (showEnabledColumn && !detailLayout)
            {
                ImGui.TableSetupColumn(
                    "##enabled",
                    ImGuiTableColumnFlags.WidthFixed,
                    OmniControls.MeasureCheckbox(string.Empty).X);
            }

            ImGui.TableSetupColumn(OmniLoc.Get("Common.Item"), ImGuiTableColumnFlags.WidthStretch);
            if (!detailLayout)
            {
                ImGui.TableSetupColumn(
                    OmniLoc.Get("Common.Action"),
                    ImGuiTableColumnFlags.WidthFixed,
                    deleteButtonSize.X);
            }

            ImGui.TableSetupScrollFreeze(0, 1);
            var change = DrawHeader(rows, showEnabledColumn, detailLayout);
            ImGui.TableSetColumnIndex(!detailLayout && showEnabledColumn ? 1 : 0);
            nameWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
            rowContentHeight = MathF.Max(iconSize, MathF.Max(OmniTheme.CheckboxSize(), deleteButtonSize.Y));
            foreach (var row in rows)
                rowContentHeight = MathF.Max(rowContentHeight, MeasureItemCellHeight(LuminaWrapper.GetItemName(row.ItemID), nameWidth, iconSize));
            rowHeight = rowContentHeight + cellPadding;
            if (detailLayout)
            {
                foreach (var row in rows)
                {
                    ImGui.TableNextRow();
                    var rowChange = DrawRow(row, rowContentHeight, iconSize, deleteButtonSize, showEnabledColumn, true);
                    if (rowChange.Action != ItemSelectionTableAction.None)
                    {
                        change = rowChange;
                    }
                }
                return change;
            }
            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(rows.Count, rowHeight);
            while (clipper.Step())
            {
                for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, rowContentHeight);
                    var rowChange = DrawRow(rows[index], rowContentHeight, iconSize, deleteButtonSize, showEnabledColumn, false);
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
        bool showEnabledColumn,
        bool detailLayout)
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

            if (!detailLayout)
                OmniControls.CenterTableItem(new Vector2(OmniTheme.CheckboxSize()), OmniTheme.SmallButtonSize().Y);
            using (ImRaii.Disabled(rows.Count == 0))
            {
                if (OmniControls.Checkbox("##toggleAll", ref allEnabled))
                {
                    return new(ItemSelectionTableAction.SetAll, Enabled: allEnabled);
                }
            }
        }

        if (detailLayout)
        {
            if (showEnabledColumn)
                OmniControls.SameLineOrWrap(ImGui.CalcTextSize(OmniLoc.Get("Common.Item")).X);
            else
                ImGui.TableNextColumn();
            ImGui.TextUnformatted(OmniLoc.Get("Common.Item"));
        }
        else
        {
            OmniControls.TableHeader(OmniLoc.Get("Common.Item"));
            OmniControls.TableHeader(OmniLoc.Get("Common.Action"));
        }
        return default;
    }

    private static ItemSelectionTableChange DrawRow(
        ItemSelectionTableRow row,
        float rowContentHeight,
        float iconSize,
        Vector2 deleteButtonSize,
        bool showEnabledColumn,
        bool detailLayout)
    {
        ImGui.PushID((int)row.ItemID);
        var change = default(ItemSelectionTableChange);
        if (showEnabledColumn)
        {
            ImGui.TableNextColumn();
            var enabled = row.Enabled;
            if (!detailLayout)
                OmniControls.CenterTableItem(new Vector2(OmniTheme.CheckboxSize()), rowContentHeight);
            if (OmniControls.Checkbox("##enabled", ref enabled))
            {
                change = new(ItemSelectionTableAction.SetItem, row.ItemID, enabled);
            }
        }

        if (!detailLayout || !showEnabledColumn)
            ImGui.TableNextColumn();
        DrawItemCell(row.ItemID, rowContentHeight, iconSize, wrapName: true);

        if (!detailLayout)
            ImGui.TableNextColumn();
        if (row.CanDelete)
        {
            if (!detailLayout)
                OmniControls.CenterTableItem(deleteButtonSize, rowContentHeight);
            if (OmniControls.SmallButton(OmniLoc.Get("Common.Delete"), false, deleteButtonSize))
            {
                change = new(ItemSelectionTableAction.Delete, row.ItemID);
            }
        }
        else
        {
            OmniControls.TableTextCentered("-", detailLayout ? 0f : rowContentHeight);
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
        var textSpacing = OmniTheme.Scale(6f);
        var stacked = wrapName && availableWidth < iconSize + textSpacing + ImGui.GetFontSize() * 2f;
        var cellHeight = wrapName ? MathF.Max(rowContentHeight, MeasureItemCellHeight(itemName, availableWidth, iconSize)) : rowContentHeight;
        var cellPosition = ImGui.GetCursorScreenPos();
        var iconPosition = new Vector2(
            cellPosition.X,
            cellPosition.Y + (stacked ? 0f : MathF.Max(0f, (cellHeight - iconSize) * 0.5f)));
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

        var wrapWidth = wrapName ? MathF.Max(1f, availableWidth - (stacked ? 0f : iconSize + textSpacing)) : 0f;
        var textSize = ImGui.CalcTextSize(itemName, false, wrapWidth);
        var textPosition = new Vector2(
            cellPosition.X + (stacked ? 0f : iconSize + textSpacing),
            cellPosition.Y + (stacked ? iconSize + textSpacing : MathF.Max(0f, (cellHeight - textSize.Y) * 0.5f)));
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), textPosition,
            ImGui.GetColorU32(ImGuiCol.Text), itemName, wrapWidth);
        ImGui.SetCursorScreenPos(textPosition);
        ImGui.Dummy(new Vector2(
            MathF.Max(1f, MathF.Min(textSize.X, wrapName ? wrapWidth : availableWidth - iconSize - textSpacing)),
            textSize.Y));
        if (showItemIDTooltip)
        {
            OmniControls.HelpTooltip(string.Format(OmniLoc.Get("Common.ItemId"), itemID));
        }
    }

    public static float MeasureItemCellHeight(string name, float width, float iconSize)
    {
        var spacing = OmniTheme.Scale(6f);
        var stacked = width < iconSize + spacing + ImGui.GetFontSize() * 2f;
        var textHeight = ImGui.CalcTextSize(name, false, MathF.Max(1f, width - (stacked ? 0f : iconSize + spacing))).Y;
        return stacked ? iconSize + spacing + textHeight : MathF.Max(iconSize, textHeight);
    }
}
