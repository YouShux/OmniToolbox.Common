using Dalamud.Utility;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;
using TreeToolKit;

namespace OmniToolbox.UI.Controls;

public static class ItemSelectionTable
{
    private static readonly Dictionary<uint, ItemLayout> Layouts = [];

    internal static void ClearDisplayCaches() => Layouts.Clear();

    public static ItemSelectionTableChange Draw
    (
        string                               id,
        IReadOnlyList<ItemSelectionTableRow> rows,
        bool                                 showEnabledColumn = true
    )
    {
        var layoutID = ImGui.GetID(id);
        if (!Layouts.TryGetValue(layoutID, out var layout))
        {
            if (Layouts.Count >= 128)
                Layouts.Clear();
            layout = new();
            Layouts.Add(layoutID, layout);
        }
        layout.UpdateItems(rows);
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
        rowContentHeight = MathF.Max(rowContentHeight, layout.Measure(nameWidth, iconSize, false));
        var cellPadding = ImGui.GetStyle().CellPadding.Y * 2f;
        var rowHeight = rowContentHeight + cellPadding;
        var tableHeight = OmniTheme.SmallButtonSize().Y + cellPadding +
                          rowHeight * Math.Clamp(rows.Count, 1, 5) + OmniTheme.BorderThickness() * 2f;

        using var idScope = ImRaii.PushId(id);
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
        var interaction = new OmniListInteraction("##itemSelectionRows");
        ImGui.TableSetColumnIndex(!detailLayout && showEnabledColumn ? 1 : 0);
        nameWidth        = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        rowContentHeight = MathF.Max(iconSize, MathF.Max(OmniTheme.CheckboxSize(), deleteButtonSize.Y));
        rowContentHeight = MathF.Max(rowContentHeight, layout.Measure(nameWidth, iconSize, true));
        rowHeight        = rowContentHeight + cellPadding;
        if (detailLayout)
        {
            var measuredDetails = layout.DetailWidth == nameWidth && layout.DetailRows.Length == rows.Count &&
                layout.DetailSpacing == ImGui.GetStyle().ItemSpacing && layout.DetailPadding == ImGui.GetStyle().CellPadding &&
                layout.DetailEnabled == showEnabledColumn;
            if (!measuredDetails)
            {
                layout.DetailWidth = nameWidth;
                layout.DetailSpacing = ImGui.GetStyle().ItemSpacing;
                layout.DetailPadding = ImGui.GetStyle().CellPadding;
                layout.DetailEnabled = showEnabledColumn;
                layout.DetailRows = new float[rows.Count];
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                ImGui.TableNextRow(ImGuiTableRowFlags.None, layout.DetailRows[index]);
                ImGui.TableSetColumnIndex(0);
                var top = ImGui.GetCursorPosY();
                if (measuredDetails && !interaction.ShouldRetain(row.ItemID) &&
                    !ImGui.IsRectVisible(new Vector2(nameWidth, layout.DetailRows[index])))
                {
                    continue;
                }
                using var rowScope = interaction.BeginRow(row.ItemID);
                var rowChange = DrawRow(row, layout.Names[index], rowContentHeight, iconSize, deleteButtonSize, showEnabledColumn, true, true);
                if (!measuredDetails)
                    layout.DetailRows[index] = MathF.Max(rowContentHeight, ImGui.GetCursorPosY() - top - ImGui.GetStyle().ItemSpacing.Y);
                if (rowChange.Action != ItemSelectionTableAction.None)
                {
                    change = rowChange;
                }
            }
            return change;
        }
        var clipper = ImGui.ImGuiListClipper();
        clipper.Begin(rows.Count, rowHeight);
        if (interaction.RetainAll)
            clipper.ForceDisplayRangeByIndices(0, rows.Count);
        else if (interaction.HasRetainedRows)
            for (var index = 0; index < rows.Count; index++)
                interaction.IncludeRetainedRow(clipper, index, rows[index].ItemID);
        while (clipper.Step())
        {
            for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowContentHeight);
                using var rowScope = interaction.BeginRow(rows[index].ItemID);
                var rowChange = DrawRow(rows[index], layout.Names[index], rowContentHeight, iconSize, deleteButtonSize, showEnabledColumn, false);
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

    private static ItemSelectionTableChange DrawHeader
    (
        IReadOnlyList<ItemSelectionTableRow> rows,
        bool                                 showEnabledColumn,
        bool                                 detailLayout
    )
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
                OmniControls.SameLineOrWrap(OmniTextMeasurements.Measure(OmniLoc.Get("Common.Item")).X);
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

    private static ItemSelectionTableChange DrawRow
    (
        ItemSelectionTableRow row,
        string                name,
        float                 rowContentHeight,
        float                 iconSize,
        Vector2               deleteButtonSize,
        bool                  showEnabledColumn,
        bool                  detailLayout,
        bool                  firstColumnReady = false
    )
    {
        using var idScope = ImRaii.PushId((int)row.ItemID);
        var change = default(ItemSelectionTableChange);
        if (showEnabledColumn)
        {
            if (!firstColumnReady)
                ImGui.TableNextColumn();
            var enabled = row.Enabled;
            if (!detailLayout)
                OmniControls.CenterTableItem(new Vector2(OmniTheme.CheckboxSize()), rowContentHeight);
            if (OmniControls.Checkbox("##enabled", ref enabled))
            {
                change = new(ItemSelectionTableAction.SetItem, row.ItemID, enabled);
            }
        }

        if ((!detailLayout || !showEnabledColumn) && (!firstColumnReady || showEnabledColumn))
            ImGui.TableNextColumn();
        DrawItemCell(row.ItemID, rowContentHeight, iconSize, displayName: name, wrapName: true);

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

        return change;
    }

    public static void DrawItemCell
    (
        uint    itemID,
        float   rowContentHeight,
        float   iconSize,
        string? displayName = null,
        bool    showItemIDTooltip = true,
        bool    isCollected = false,
        bool    wrapName = false
    ) => DrawItemCell(itemID, rowContentHeight, iconSize, false, displayName, showItemIDTooltip, isCollected, wrapName);

    public static void DrawItemCell
    (
        uint    itemID,
        float   rowContentHeight,
        float   iconSize,
        bool    itemHQ,
        string? displayName = null,
        bool    showItemIDTooltip = true,
        bool    isCollected = false,
        bool    wrapName = false
    )
    {
        var (baseItemID, kind) = ItemUtil.GetBaseId(itemID);
        itemHQ |= kind == ItemKind.Hq;
        itemID = baseItemID;
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
            FramedGameIcon.DrawItem(item, new Vector2(iconSize), itemHQ);
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
        var textSize = OmniTextMeasurements.Measure(itemName, false, wrapWidth);
        var textPosition = new Vector2(
            cellPosition.X + (stacked ? 0f : iconSize + textSpacing),
            cellPosition.Y + (stacked ? iconSize + textSpacing : MathF.Max(0f, (cellHeight - textSize.Y) * 0.5f)));
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), textPosition,
            ImGui.GetColorU32(ImGuiCol.Text), itemName, wrapWidth);
        ImGui.SetCursorScreenPos(textPosition);
        ImGui.Dummy(new Vector2(
            MathF.Max(1f, MathF.Min(textSize.X, wrapName ? wrapWidth : availableWidth - iconSize - textSpacing)),
            textSize.Y));
        if (showItemIDTooltip && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            OmniControls.HelpTooltip(string.Format(OmniLoc.Get("Common.ItemId"), itemID));
        }
    }

    public static float MeasureItemCellHeight
    (
        string name,
        float  width,
        float  iconSize
    )
    {
        var spacing = OmniTheme.Scale(6f);
        var stacked = width < iconSize + spacing + ImGui.GetFontSize() * 2f;
        var textHeight = OmniTextMeasurements.Measure(name, false, MathF.Max(1f, width - (stacked ? 0f : iconSize + spacing))).Y;
        return stacked ? iconSize + spacing + textHeight : MathF.Max(iconSize, textHeight);
    }
    private sealed class ItemLayout
    {
        private readonly List<uint>                                                     itemIDs   = [];
        private readonly List<bool> deletableItems = [];
        internal readonly List<string> Names = [];
        private (float Width, float IconSize, float FontSize, nint Font, float Height)? estimated;
        private (float Width, float IconSize, float FontSize, nint Font, float Height)? actual;
        private long fontRevision = -1;
        private long localizationRevision = -1;
        internal float DetailWidth;
        internal float[] DetailRows = [];
        internal Vector2 DetailSpacing;
        internal Vector2 DetailPadding;
        internal bool DetailEnabled;

        public void UpdateItems(IReadOnlyList<ItemSelectionTableRow> rows)
        {
            var same = rows.Count == itemIDs.Count && fontRevision == Fonts.Revision && localizationRevision == OmniLoc.Revision;
            for (var index = 0; same && index < rows.Count; index++)
                same = rows[index].ItemID == itemIDs[index] && rows[index].CanDelete == deletableItems[index];
            if (same)
                return;

            fontRevision = Fonts.Revision;
            localizationRevision = OmniLoc.Revision;
            itemIDs.Clear();
            deletableItems.Clear();
            Names.Clear();
            estimated = actual = null;
            DetailRows = [];
            foreach (var row in rows)
            {
                itemIDs.Add(row.ItemID);
                deletableItems.Add(row.CanDelete);
                var (baseItemID, _) = ItemUtil.GetBaseId(row.ItemID);
                var name = LuminaWrapper.GetItemName(baseItemID);
                Names.Add(string.IsNullOrWhiteSpace(name) ? baseItemID.ToString() : name);
            }
        }

        public unsafe float Measure
        (
            float width,
            float iconSize,
            bool  useActual
        )
        {
            ref var cached = ref (useActual ? ref actual : ref estimated);
            var font = (nint)ImGui.GetFont().Handle;
            var fontSize = ImGui.GetFontSize();
            if (cached is { } layout && layout.Width == width && layout.IconSize == iconSize &&
                layout.FontSize == fontSize && layout.Font == font)
                return layout.Height;

            if (useActual)
                DetailRows = [];
            var height = iconSize;
            foreach (var name in Names)
                height = MathF.Max(height, MeasureItemCellHeight(name, width, iconSize));
            cached = (width, iconSize, fontSize, font, height);
            return height;
        }
    }

}
