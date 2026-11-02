using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI;

public static class OmniRecordHistoryMenu
{
    public static void Draw<T>
    (
        string                      popupID,
        IReadOnlyList<T>            items,
        IOmniRecordHistorySource<T> source
    )
    {
        if (!ImGui.IsPopupOpen(popupID))
            return;
        var fontSize = ImGui.GetFontSize();
        using var scale = new OmniTheme.ScaleScope(fontSize / OmniTheme.REFERENCE_FONT_SIZE);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, OmniTheme.Scale(new Vector2(6f)))
            .Push(ImGuiStyleVar.FramePadding, OmniTheme.Scale(new Vector2(4f, 0f)))
            .Push(ImGuiStyleVar.CellPadding, OmniTheme.Scale(new Vector2(3f, 2f)))
            .Push(ImGuiStyleVar.ItemSpacing, OmniTheme.Scale(new Vector2(5f, 4f)))
            .Push(ImGuiStyleVar.FrameRounding, OmniTheme.Scale(2f))
            .Push(ImGuiStyleVar.FrameBorderSize, OmniTheme.Scale(1f));
        var labels = source.Labels;
        var style = ImGui.GetStyle();
        var buttonWidth = MathF.Ceiling(MathF.Max(ImGui.CalcTextSize(labels.Import).X,
            ImGui.CalcTextSize(labels.Export).X) + style.FramePadding.X * 2f);
        var currentWidth = string.IsNullOrEmpty(labels.Current)
            ? 0f : MathF.Ceiling(ImGui.CalcTextSize(labels.Current).X + style.FramePadding.X * 2f);
        var labelWidth = 0f;
        var labelHeight = ImGui.GetTextLineHeight();
        foreach (var item in items)
        {
            var labelSize = ImGui.CalcTextSize(source.GetLabel(item));
            labelWidth = MathF.Max(labelWidth, labelSize.X);
            labelHeight = MathF.Max(labelHeight, labelSize.Y);
        }
        labelWidth = MathF.Ceiling(labelWidth + style.FramePadding.X * 2f);
        var recordsWidth = MathF.Ceiling(labelWidth + buttonWidth + style.CellPadding.X * 4f + 3f);
        var contentWidth = MathF.Max(currentWidth, source.HeaderWidth) + style.ItemSpacing.X + buttonWidth;
        if (items.Count > 0)
            contentWidth = MathF.Max(contentWidth, recordsWidth);
        else
            contentWidth = MathF.Max(contentWidth, ImGui.CalcTextSize(labels.Empty).X);
        var workSize = OmniWindowPlacement.GetWorkArea().Size;
        var rowHeight = labelHeight + style.CellPadding.Y * 2f;
        var listHeight = MathF.Max(1f, MathF.Min(workSize.Y * 0.6f, rowHeight * MAX_VISIBLE_RECORDS));
        var scrollList = items.Count > MAX_VISIBLE_RECORDS || rowHeight * items.Count > listHeight;
        var minimumSize = Vector2.Min(new Vector2(contentWidth + style.WindowPadding.X * 2f +
            style.WindowBorderSize * 2f + (scrollList ? style.ScrollbarSize : 0f), 0f), workSize);
        ImGui.SetNextWindowSize(new Vector2(minimumSize.X, 0f), ImGuiCond.Always);
        OmniControls.SetNextAutoResizeWindowSizeConstraints(minimumSize, workSize);
        using var popup = ImRaii.Popup(popupID);
        if (!popup)
            return;
        OmniRecordWindow.SetWindowFontSize(fontSize);
        source.DrawHeader();
        if (currentWidth > 0f)
        {
            if (ImGui.Selectable(labels.Current, source.IsCurrentSelected, ImGuiSelectableFlags.None,
                    new Vector2(currentWidth, ImGui.GetTextLineHeight())))
                source.SelectCurrent();

            ImGui.SameLine(0f, style.ItemSpacing.X);
        }
        using (ImRaii.Disabled(!source.CanTransfer))
            if (ImGui.SmallButton(labels.Import))
                source.Import();

        if (items.Count == 0)
            ImGui.TextDisabled(labels.Empty);
        else if (scrollList || recordsWidth > ImGui.GetContentRegionAvail().X)
        {
            var horizontalScroll = recordsWidth > ImGui.GetContentRegionAvail().X;
            var height = MathF.Min(listHeight, rowHeight * items.Count) + (horizontalScroll ? style.ScrollbarSize : 0f);
            ImGui.SetNextWindowContentSize(new Vector2(recordsWidth, 0f));
            using var child = ImRaii.Child("##Records", new Vector2(0f, height), false, ImGuiWindowFlags.HorizontalScrollbar);
            if (child)
            {
                OmniRecordWindow.SetWindowFontSize(fontSize);
                DrawItems(items, source, labelWidth, buttonWidth, recordsWidth, labels.Export);
            }
        }
        else
            DrawItems(items, source, labelWidth, buttonWidth, recordsWidth, labels.Export);
        source.DrawFooter();
    }

    private static void DrawItems<T>
    (
        IReadOnlyList<T>            items,
        IOmniRecordHistorySource<T> source,
        float                       labelWidth,
        float                       buttonWidth,
        float                       width,
        string                      exportLabel
    )
    {
        using var table = OmniControls.DataTable("##RecordsTable", [string.Empty, string.Empty],
            [labelWidth, buttonWidth], [labelWidth, buttonWidth], out _,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX, size: new Vector2(width, 0f));
        if (!table)
            return;
        foreach (var item in items)
        {
            using var id = ImRaii.PushId(source.GetID(item));
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (ImGui.Selectable($"{source.GetLabel(item)}##Record", source.IsSelected(item)))
                source.Select(item);

            ImGui.TableNextColumn();
            using var disabled = ImRaii.Disabled(!source.CanTransfer);
            if (ImGui.SmallButton($"{exportLabel}##Export"))
                source.Export(item);
        }
    }

    public readonly record struct Labels(string Current, string Import, string Export, string Empty);

    #region 常量

    private const int MAX_VISIBLE_RECORDS = 5;

    #endregion
}
