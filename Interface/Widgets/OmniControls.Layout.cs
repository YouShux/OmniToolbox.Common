using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Utility;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public static partial class OmniControls
{
    public static unsafe void SetNextAutoResizeWindowSizeConstraints(Vector2 minimumSize, Vector2 maximumSize)
    {
        // ImGui 会向下取整约束尺寸；高度向上取整以容纳缩放后的非整数内边距。
        maximumSize.Y = MathF.Floor(maximumSize.Y);
        ImGui.SetNextWindowSizeConstraints(minimumSize, maximumSize,
            static data => data->DesiredSize.Y = MathF.Ceiling(data->DesiredSize.Y));
    }

    public static ImRaii.TableDisposable DataTable(string id, ReadOnlySpan<string> labels,
        ReadOnlySpan<float> minimumWidths, ReadOnlySpan<float> desiredWidths, out bool detailLayout,
        ImGuiTableFlags flags, Vector2 size = default, int stretchColumn = 0) =>
        DataTable(id, labels, minimumWidths, desiredWidths, out detailLayout, flags, -1, size, stretchColumn);

    public static unsafe ImRaii.TableDisposable DataTable(string id, ReadOnlySpan<string> labels,
        ReadOnlySpan<float> minimumWidths, ReadOnlySpan<float> desiredWidths, out bool detailLayout,
        ImGuiTableFlags flags, int secondStretchColumn, Vector2 size = default, int stretchColumn = 0)
    {
        var width = size.X > 0f ? size.X : ImGui.GetContentRegionAvail().X;
        var contentWidth = width - ImGui.GetStyle().CellPadding.X * labels.Length * 2f - labels.Length - 1f;
        if ((flags & ImGuiTableFlags.ScrollY) != 0)
            contentWidth -= ImGui.GetStyle().ScrollbarSize;
        Span<float> requiredWidths = stackalloc float[labels.Length];
        for (var index = 0; index < labels.Length; index++)
            requiredWidths[index] = MathF.Max(minimumWidths[index], ImGui.CalcTextSize(DisplayLabel(labels[index])).X);
        var equalColumns = (flags & ImGuiTableFlags.SizingMask) == ImGuiTableFlags.SizingStretchSame;
        if (equalColumns)
        {
            var minimumWidth = 0f;
            foreach (var requiredWidth in requiredWidths)
                minimumWidth = MathF.Max(minimumWidth, requiredWidth);
            requiredWidths.Fill(MathF.Ceiling(minimumWidth));
        }
        if (secondStretchColumn >= 0)
            requiredWidths[stretchColumn] = requiredWidths[secondStretchColumn] =
                MathF.Max(requiredWidths[stretchColumn], requiredWidths[secondStretchColumn]);
        var innerWidth = 0f;
        if ((flags & ImGuiTableFlags.ScrollX) != 0)
        {
            contentWidth = 0f;
            for (var index = 0; index < labels.Length; index++)
                contentWidth += MathF.Ceiling(MathF.Max(requiredWidths[index], desiredWidths[index]));
            innerWidth = contentWidth + ImGui.GetStyle().CellPadding.X * labels.Length * 2f + labels.Length + 1f;
        }
        Span<float> widths = stackalloc float[labels.Length];
        detailLayout = !FitColumns(requiredWidths, desiredWidths, contentWidth, widths, stretchColumn);
        if (!detailLayout && secondStretchColumn >= 0)
            widths[stretchColumn] = widths[secondStretchColumn] =
                (widths[stretchColumn] + widths[secondStretchColumn]) * 0.5f;
        var table = ImRaii.Table(id, detailLayout ? 1 : labels.Length, flags, size, innerWidth);
        if (table)
        {
            if (detailLayout)
                ImGui.TableSetupColumn(labels[0], ImGuiTableColumnFlags.WidthStretch);
            else
            {
                var nativeTable = ImGuiP.GetCurrentTable();
                var refit = false;
                var fixedWidth = 0f;
                for (var index = 0; index < labels.Length; index++)
                {
                    ref var column = ref nativeTable.Columns.Data[index];
                    if (equalColumns)
                    {
                        ImGui.TableSetupColumn(labels[index], ImGuiTableColumnFlags.WidthStretch, 1f);
                        column.StretchWeight = 1f;
                        continue;
                    }
                    refit |= MathF.Abs(column.InitStretchWeightOrWidth - widths[index]) > 0.5f;
                    if (index != stretchColumn && index != secondStretchColumn)
                    {
                        refit |= (column.Flags & ImGuiTableColumnFlags.WidthStretch) != 0 ||
                            column.WidthRequest < MathF.Ceiling(requiredWidths[index]);
                        fixedWidth += MathF.Max(0f, column.WidthRequest);
                    }
                    ImGui.TableSetupColumn(labels[index], index == stretchColumn || index == secondStretchColumn
                        ? ImGuiTableColumnFlags.WidthStretch : ImGuiTableColumnFlags.WidthFixed, widths[index]);
                }
                if (secondStretchColumn >= 0)
                {
                    nativeTable.Columns.Data[stretchColumn].StretchWeight = 1f;
                    nativeTable.Columns.Data[secondStretchColumn].StretchWeight = 1f;
                }
                // 固定列使用像素宽度；布局变化或旧宽度失效时更新，稳定布局保留手动列宽。
                if (refit || ((flags & ImGuiTableFlags.ScrollX) == 0 &&
                    fixedWidth > contentWidth - (stretchColumn >= 0 ? MathF.Ceiling(requiredWidths[stretchColumn]) : 0f) -
                    (secondStretchColumn >= 0 ? MathF.Ceiling(requiredWidths[secondStretchColumn]) : 0f)))
                {
                    for (var index = 0; index < labels.Length; index++)
                    {
                        if (index == stretchColumn || index == secondStretchColumn)
                            continue;
                        ref var column = ref nativeTable.Columns.Data[index];
                        column.WidthRequest = widths[index];
                        column.AutoFitQueue = 0;
                    }
                }
            }
        }
        return table;
    }

    public static int ColumnsThatFit(int maximumColumns, float minimumWidth) =>
        Math.Clamp((int)(ImGui.GetContentRegionAvail().X /
            MathF.Max(1f, minimumWidth + ImGui.GetStyle().CellPadding.X * 2f)), 1, maximumColumns);

    public static void NextTableField(string label, bool detailLayout)
    {
        ImGui.TableNextColumn();
        if (detailLayout)
        {
            using var wrap = ImRaii.TextWrapPos(0f);
            ImGui.TextDisabled(label);
        }
    }

    public static unsafe void TableWorldNameCentered(string worldName, float contentHeight)
    {
        using var rented = new RentedSeStringBuilder();
        var icon = rented.Builder
            .AppendIcon((uint)BitmapFontIcon.CrossWorld)
            .ToReadOnlySeString();
        var textWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X - ImGui.GetTextLineHeight());
        var textSize = ImGui.CalcTextSize(worldName, false, textWidth);
        var height = MathF.Max(contentHeight, textSize.Y);
        var style = new SeStringDrawParams
        {
            TargetDrawList = default(ImDrawListPtr),
            ScreenOffset = Vector2.Zero,
            Font = ImGui.GetFont(),
            FontSize = ImGui.GetFontSize(),
            WrapWidth = float.MaxValue
        };
        var iconSize = ImGuiHelpers.SeStringWrapped(icon, style).Size;
        var position = ImGui.GetCursorScreenPos();
        var groupWidth = iconSize.X + textSize.X;
        var groupLeft = position.X + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - groupWidth) * 0.5f);
        var textTop = position.Y + (height - textSize.Y) * 0.5f;
        var textCenter = textSize.Y * 0.5f;
        if (worldName.Length > 0)
        {
            var font = ImGui.GetFont();
            var glyph = font.FindGlyph(worldName[0]);
            if (glyph is not null)
                textCenter = (glyph->Y0 + glyph->Y1) * 0.5f * ImGui.GetFontSize() / font.FontSize;
        }

        style.TargetDrawList = ImGui.GetWindowDrawList();
        style.ScreenOffset = new Vector2(groupLeft, textTop + textCenter - iconSize.Y * 0.5f);
        ImGuiHelpers.SeStringWrapped(icon, style);
        ImGui.GetWindowDrawList().AddText(
            ImGui.GetFont(), ImGui.GetFontSize(),
            new Vector2(groupLeft + iconSize.X, textTop),
            ImGui.GetColorU32(ImGuiCol.Text),
            worldName, textWidth);
        ImGui.Dummy(new Vector2(0f, height));
    }

    public static bool WrappedSelectable(string label, bool selected = false,
        ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, Vector2 size = default)
    {
        var width = MathF.Max(1f, size.X > 0f ? size.X : ImGui.GetContentRegionAvail().X);
        var text = DisplayLabel(label);
        var textSize = ImGui.CalcTextSize(text, false, width);
        size = new(width, MathF.Max(size.Y, textSize.Y));
        var position = ImGui.GetCursorScreenPos();
        bool clicked;
        using (ImRaii.PushColor(ImGuiCol.Text, Vector4.Zero))
            clicked = ImGui.Selectable(label, selected, flags, size);
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(),
            position + new Vector2(0f, MathF.Max(0f, (size.Y - textSize.Y) * 0.5f)),
            ImGui.GetColorU32(ImGuiCol.Text), text, width);
        return clicked;
    }

    public static ImRaii.TableDisposable SettingsTable(string id, ReadOnlySpan<Vector2> groups,
        ReadOnlySpan<string> labels, ReadOnlySpan<float> weights = default,
        ImGuiTableFlags flags = ImGuiTableFlags.SizingStretchProp) =>
        SettingsTable(id, groups, labels, 0, weights, flags);

    public static unsafe ImRaii.TableDisposable SettingsTable(string id, ReadOnlySpan<Vector2> groups,
        ReadOnlySpan<string> labels, int columnsPerRow, ReadOnlySpan<float> weights = default,
        ImGuiTableFlags flags = ImGuiTableFlags.SizingStretchProp)
    {
        var layoutColumns = columnsPerRow > 0 ? columnsPerRow : groups.Length;
        var groupCount = groups.Length;
        while (groupCount > 1 && groups[groupCount - 1] == Vector2.Zero)
            groupCount--;
        groups = groups[..groupCount];
        var available = ImGui.GetContentRegionAvail().X;
        var innerPadding = (flags & ImGuiTableFlags.NoPadInnerX) == 0 ? ImGui.GetStyle().CellPadding.X * 2f : 0f;
        var outerPadding = (flags & ImGuiTableFlags.NoPadOuterX) == 0 ? ImGui.GetStyle().CellPadding.X * 2f : 0f;
        var fixedColumns = (flags & ImGuiTableFlags.SizingMask) == ImGuiTableFlags.SizingFixedFit;
        var columns = Math.Min(layoutColumns, groups.Length);
        Span<float> widths = stackalloc float[columns];
        Span<float> columnWeights = stackalloc float[columns];
        var requiredWidth = 0f;
        var totalWeight = 0f;
        for (; columns > 0; columns--)
        {
            widths.Clear();
            columnWeights.Clear();
            for (var index = 0; index < groups.Length; index++)
            {
                var column = columnsPerRow > 0 ? index % layoutColumns % columns : index % columns;
                widths[column] = MathF.Max(widths[column], MathF.Ceiling(groups[index].X));
                if (groups[index].X > 0f)
                    columnWeights[column] = MathF.Max(columnWeights[column], weights.IsEmpty ? 1f : weights[index]);
            }
            requiredWidth = innerPadding * (columns - 1) + outerPadding;
            totalWeight = 0f;
            for (var index = 0; index < columns; index++)
            {
                requiredWidth += widths[index];
                totalWeight += columnWeights[index];
            }
            if (requiredWidth <= available + 1f || columns == 1)
                break;
        }
        var extraWidth = MathF.Max(0f, available - requiredWidth);
        if (columnsPerRow > 0 && !fixedColumns)
        {
            var contentWidth = available - innerPadding * (columns - 1) - outerPadding;
            var slotColumns = columns < Math.Min(layoutColumns, groups.Length) ? columns : layoutColumns;
            var slotWidth = MathF.Max(1f,
                (available - innerPadding * (slotColumns - 1) - outerPadding) / slotColumns);
            Span<float> desiredWidths = stackalloc float[columns];
            for (var index = 0; index < columns; index++)
                desiredWidths[index] = MathF.Max(widths[index], slotWidth);
            FitColumns(widths[..columns], desiredWidths, contentWidth, widths[..columns], columns - 1);
            extraWidth = 0f;
        }
        flags = flags & ~ImGuiTableFlags.SizingMask |
                (fixedColumns ? ImGuiTableFlags.SizingFixedFit : ImGuiTableFlags.SizingStretchProp) | ImGuiTableFlags.NoSavedSettings;
        var table = ImRaii.Table(id, columns, flags, new Vector2(available, 0f));
        if (table)
        {
            var nativeTable = ImGuiP.GetCurrentTable();
            for (var index = 0; index < columns; index++)
            {
                if (fixedColumns && columns == 1)
                    widths[index] = MathF.Min(widths[index], MathF.Max(1f, available - outerPadding));
                var columnWidth = MathF.Max(1f, widths[index] +
                    (!fixedColumns && totalWeight > 0f ? extraWidth * columnWeights[index] / totalWeight : 0f));
                ImGui.TableSetupColumn(index < labels.Length ? labels[index] : string.Empty,
                    fixedColumns ? ImGuiTableColumnFlags.WidthFixed : ImGuiTableColumnFlags.WidthStretch,
                    columnWidth);
                ref var column = ref nativeTable.Columns.Data[index];
                if (fixedColumns)
                {
                    column.WidthRequest = columnWidth;
                    column.AutoFitQueue = 0;
                }
                else
                {
                    column.StretchWeight = columnWidth;
                }
            }
        }
        return table;
    }

    private static void WrapControl(float width)
    {
        if (ImGuiP.GetCurrentWindow().DC.IsSameLine != 0 && width > ImGui.GetContentRegionAvail().X + 1f)
            ImGui.NewLine();
    }

    public static Vector2 MeasureCheckbox(string label, float controlSize = 0f)
    {
        var side = MathF.Max(ImGui.GetTextLineHeight(), controlSize > 0f ? controlSize : OmniTheme.CheckboxSize());
        var text = DisplayLabel(label);
        var textSize = ImGui.CalcTextSize(text);
        return new(MathF.Ceiling(side + (text.Length == 0 ? 0f : ImGui.GetStyle().ItemInnerSpacing.X + textSize.X)),
            MathF.Max(side, textSize.Y));
    }

    public static Vector2 MeasureTableHeader(string label, bool hasTooltip = false)
    {
        var size = ImGui.CalcTextSize(DisplayLabel(label));
        if (hasTooltip)
        {
            var icon = GetScaledIconSize(FontAwesomeIcon.InfoCircle.ToIconString());
            size.X += OmniTheme.Scale(4f) + icon.X;
            size.Y = MathF.Max(size.Y, icon.Y);
        }
        return new(MathF.Ceiling(size.X), MathF.Ceiling(size.Y));
    }

    public static Vector2 MeasureTableHeader(string label, string status)
    {
        var size = MeasureGroup([ImGui.CalcTextSize(DisplayLabel(label)), ImGui.CalcTextSize($"[{status}]")], OmniTheme.Scale(4f));
        return new(MathF.Ceiling(size.X), MathF.Ceiling(size.Y));
    }

    public static Vector2 MeasureCombo(string preview, float minimumWidth = 0f)
    {
        var height = MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight());
        return new(MathF.Max(minimumWidth, ImGui.CalcTextSize(preview).X + ImGui.GetStyle().FramePadding.X * 2f + height), height);
    }

    public static Vector2 MeasureInput(string displayText, float minimumWidth = 0f, int step = 0)
    {
        var height = MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight());
        var width = MathF.Max(minimumWidth, ImGui.CalcTextSize(displayText).X + ImGui.GetStyle().FramePadding.X * 2f);
        if (step != 0)
        {
            width += (height + ImGui.GetStyle().ItemInnerSpacing.X) * 2f;
        }
        return new(width, height);
    }

    public static Vector2 MeasureSteppedInput(string displayText, float minimumWidth = 0f)
    {
        var input = MeasureInput(displayText, minimumWidth);
        return MeasureGroup([input, new(input.Y), new(input.Y)], OmniTheme.Scale(6f));
    }

    public static Vector2 MeasureFloatInput(float value, string format, float minimumWidth = 0f, bool stepped = false)
    {
        Span<byte> buffer = stackalloc byte[64];
        var formatted = ImGui.DataTypeFormatString(buffer, value, format);
        return MeasureInput(Encoding.UTF8.GetString(formatted),
            minimumWidth, stepped ? 1 : 0);
    }

    public static Vector2 MeasureIntInput(int value, string format, float minimumWidth = 0f)
    {
        Span<byte> buffer = stackalloc byte[64];
        var formatted = ImGui.DataTypeFormatString(buffer, value, format);
        return MeasureInput(Encoding.UTF8.GetString(formatted), minimumWidth);
    }

    public static Vector2 MeasureGroup(ReadOnlySpan<Vector2> sizes, float spacing = -1f)
    {
        spacing = spacing < 0f ? ImGui.GetStyle().ItemSpacing.X : spacing;
        var size = Vector2.Zero;
        for (var index = 0; index < sizes.Length; index++)
        {
            size.X += sizes[index].X + (index == 0 ? 0f : spacing);
            size.Y = MathF.Max(size.Y, sizes[index].Y);
        }
        return size;
    }

    // 参数为已测量的屏幕尺寸；只调整下一组的位置，不提交控件或改变最后项目。
    public static void SameLineOrWrap(float nextWidth, float spacing = -1f)
    {
        spacing = spacing < 0f ? ImGui.GetStyle().ItemSpacing.X : spacing;
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        if (MathF.Floor(ImGui.GetItemRectMax().X + spacing) + MathF.Ceiling(nextWidth) <= right + 1f)
        {
            ImGui.SameLine(0f, spacing);
        }
    }

    public static float WrappedGroupHeight(ReadOnlySpan<Vector2> sizes, float width, float spacing = -1f)
    {
        spacing = spacing < 0f ? ImGui.GetStyle().ItemSpacing.X : spacing;
        var rowWidth = 0f;
        var rowHeight = 0f;
        var height = 0f;
        foreach (var size in sizes)
        {
            if (rowWidth > 0f && rowWidth + spacing + size.X > width + 1f)
            {
                height += rowHeight + ImGui.GetStyle().ItemSpacing.Y;
                rowWidth = 0f;
                rowHeight = 0f;
            }
            rowWidth += (rowWidth == 0f ? 0f : spacing) + size.X;
            rowHeight = MathF.Max(rowHeight, size.Y);
        }
        return height + rowHeight;
    }

    public static bool FitColumns(ReadOnlySpan<float> minimum, ReadOnlySpan<float> desired, float width, Span<float> result,
        int stretchColumn = -1)
    {
        width = MathF.Floor(width);
        var minimumSum = 0f;
        var desiredSum = 0f;
        for (var index = 0; index < minimum.Length; index++)
        {
            minimumSum += MathF.Ceiling(minimum[index]);
            desiredSum += MathF.Ceiling(MathF.Max(minimum[index], desired[index]));
        }
        if (minimumSum > width)
        {
            return false;
        }
        var proportion = desiredSum > minimumSum ? Math.Clamp((width - minimumSum) / (desiredSum - minimumSum), 0f, 1f) : 0f;
        var allocated = 0f;
        for (var index = 0; index < minimum.Length; index++)
        {
            var minimumWidth = MathF.Ceiling(minimum[index]);
            result[index] = minimumWidth + MathF.Floor((MathF.Ceiling(MathF.Max(minimum[index], desired[index])) - minimumWidth) * proportion);
            allocated += result[index];
        }
        if (stretchColumn >= 0)
            result[stretchColumn] += MathF.Max(0f, width - allocated);
        return true;
    }
}
