using System.Globalization;
using System.Runtime.CompilerServices;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;
using TreeToolKit;

namespace OmniToolbox.UI;

public static class OmniRecordHistoryMenu
{
    private static readonly ConditionalWeakTable<object, HistoryLayout> Layouts = new();

    internal static void ClearDisplayCaches() => Layouts.Clear();

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
        var buttonWidth = MathF.Ceiling(MathF.Max(OmniTextMeasurements.Measure(labels.Import).X,
            OmniTextMeasurements.Measure(labels.Export).X) + style.FramePadding.X * 2f);
        var currentWidth = string.IsNullOrEmpty(labels.Current)
            ? 0f : MathF.Ceiling(OmniTextMeasurements.Measure(labels.Current).X + style.FramePadding.X * 2f);
        var layout = Layouts.GetValue(source, static _ => new());
        layout.Update(items, source);
        var labelWidth = MathF.Ceiling(layout.LabelSize.X + style.FramePadding.X * 2f);
        var labelHeight = MathF.Max(ImGui.GetTextLineHeight(), layout.LabelSize.Y);
        var recordsWidth = MathF.Ceiling(labelWidth + buttonWidth + style.CellPadding.X * 4f + 3f);
        var contentWidth = MathF.Max(currentWidth, source.HeaderWidth) + style.ItemSpacing.X + buttonWidth;
        if (items.Count > 0)
            contentWidth = MathF.Max(contentWidth, recordsWidth);
        else
            contentWidth = MathF.Max(contentWidth, OmniTextMeasurements.Measure(labels.Empty).X);
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
        using (source.CanTransfer ? null : ImRaii.Disabled())
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
                DrawItems(items, source, layout, labelWidth, buttonWidth, recordsWidth);
            }
        }
        else
            DrawItems(items, source, layout, labelWidth, buttonWidth, recordsWidth);
        source.DrawFooter();
    }

    private static void DrawItems<T>
    (
        IReadOnlyList<T>            items,
        IOmniRecordHistorySource<T> source,
        HistoryLayout              layout,
        float                       labelWidth,
        float                       buttonWidth,
        float                       width
    )
    {
        using var table = OmniControls.DataTable("##RecordsTable", [string.Empty, string.Empty],
            [labelWidth, buttonWidth], [labelWidth, buttonWidth], out _,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX, size: new Vector2(width, 0f));
        if (!table)
            return;
        var interaction = new OmniListInteraction("##recordHistoryRows");
        var retainItems = interaction.RetainAll;
        var useClipper = layout.UniformRowHeight && !retainItems;
        var clipper = ImGui.ImGuiListClipper();
        if (useClipper)
        {
            clipper.Begin(items.Count);
            if (interaction.HasRetainedRows)
                for (var index = 0; index < items.Count; index++)
                    interaction.IncludeRetainedRow(clipper, index, ImGui.GetID(layout.IDs[index]));
        }
        while (!useClipper || clipper.Step())
        {
            var first = useClipper ? clipper.DisplayStart : 0;
            var last = useClipper ? Math.Min(clipper.DisplayEnd, items.Count) : items.Count;
            for (var index = first; index < last; index++)
            {
                var item = items[index];
                var rowID = ImGui.GetID(layout.IDs[index]);
                var rowHeight = layout.RowHeights[index];
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                var nativeTable = ImGuiP.GetCurrentTable();
                if (!interaction.ShouldRetain(rowID) && (nativeTable.RowPosY2 < nativeTable.InnerClipRect.Min.Y ||
                                     nativeTable.RowPosY1 > nativeTable.InnerClipRect.Max.Y))
                {
                    continue;
                }
                using var rowScope = interaction.BeginRow(rowID);
                using var id = ImRaii.PushId(layout.IDs[index]);
                ImGui.TableNextColumn();
                if (ImGui.Selectable(layout.Labels[index], source.IsSelected(item)))
                    source.Select(item);

                ImGui.TableNextColumn();
                using var disabled = source.CanTransfer ? null : ImRaii.Disabled();
                if (ImGui.SmallButton(layout.ExportLabel))
                    source.Export(item);
            }
            if (!useClipper)
                break;
        }
        if (useClipper)
            clipper.End();
        clipper.Destroy();
    }

    public readonly record struct Labels(string Current, string Import, string Export, string Empty);

    private sealed class HistoryLayout
    {
        private long revision;
        private long localization;
        private long fontRevision;
        private nint font;
        private float fontSize;
        private string culture = string.Empty;
        private object? sourceItems;

        public string[] IDs { get; private set; } = [];

        public string[] Labels { get; private set; } = [];

        public Vector2 LabelSize { get; private set; }

        public bool UniformRowHeight { get; private set; }

        public float[] RowHeights { get; private set; } = [];
        public string ExportLabel { get; private set; } = string.Empty;

        public unsafe void Update<T>(IReadOnlyList<T> items, IOmniRecordHistorySource<T> source)
        {
            var currentFont = (nint)ImGui.GetFont().Handle;
            var currentSize = ImGui.GetFontSize();
            var currentCulture = CultureInfo.CurrentCulture.Name;
            if (source.Revision != 0 && revision == source.Revision && ReferenceEquals(sourceItems, items) &&
                IDs.Length == items.Count &&
                localization == OmniLoc.Revision && fontRevision == Fonts.Revision &&
                font == currentFont && fontSize == currentSize && culture == currentCulture)
                return;

            revision = source.Revision;
            ExportLabel = $"{source.Labels.Export}##Export";
            localization = OmniLoc.Revision;
            fontRevision = Fonts.Revision;
            font = currentFont;
            fontSize = currentSize;
            culture = currentCulture;
            sourceItems = items;
            if (IDs.Length != items.Count)
            {
                IDs = new string[items.Count];
                Labels = new string[items.Count];
                RowHeights = new float[items.Count];
            }
            LabelSize = Vector2.Zero;
            UniformRowHeight = true;
            var firstHeight = 0f;
            for (var index = 0; index < items.Count; index++)
            {
                var label = source.GetLabel(items[index]);
                IDs[index] = source.GetID(items[index]);
                Labels[index] = $"{label}##Record";
                var size = OmniTextMeasurements.Measure(label);
                LabelSize = Vector2.Max(LabelSize, size);
                RowHeights[index] = MathF.Ceiling(MathF.Max(size.Y, ImGui.GetTextLineHeight()) +
                    ImGui.GetStyle().CellPadding.Y * 2f);
                if (index == 0)
                    firstHeight = size.Y;
                else if (size.Y != firstHeight)
                    UniformRowHeight = false;
            }
        }
    }

    #region 常量

    private const int MAX_VISIBLE_RECORDS = 5;

    #endregion
}
