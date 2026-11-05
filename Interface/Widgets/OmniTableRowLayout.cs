using OmniToolbox.UI.Controls;

namespace OmniToolbox.UI.Widgets;

internal unsafe struct OmniTableRowLayout
{
    private float[]? nativeHeights;
    private int fieldIndex;
    private float startY;

    internal float Height { get; private set; }

    internal bool BeginRow(float minimumHeight, int nativeRowCount, bool retainItems)
    {
        var heights = nativeHeights;
        if (heights is null || heights.Length != nativeRowCount)
        {
            heights = new float[nativeRowCount];
            nativeHeights = heights;
            Height = 0f;
        }
        fieldIndex = 0;
        ImGui.TableNextRow(ImGuiTableRowFlags.None, Height > 0f ? heights[0] : minimumHeight);
        var table = ImGuiP.GetCurrentTable();
        startY = table.RowPosY1;
        if (Height <= 0f || retainItems ||
            startY + Height >= table.InnerClipRect.Min.Y && startY <= table.InnerClipRect.Max.Y)
            return true;

        ImGuiP.TableEndRow(table);
        for (var index = 1; index < heights.Length; index++)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, heights[index]);
            ImGuiP.TableEndRow(table);
        }
        return false;
    }

    internal void NextField(string label, bool detailLayout)
    {
        if (detailLayout)
        {
            var table = ImGuiP.GetCurrentTable();
            if (table.IsInsideRow && table.CurrentColumn >= 0)
            {
                RecordNativeRow();
                fieldIndex++;
            }
        }
        OmniControls.NextTableField(label, detailLayout);
    }

    internal void EndRow()
    {
        RecordNativeRow();
        var table = ImGuiP.GetCurrentTable();
        Height = nativeHeights is { } heights && fieldIndex + 1 == heights.Length
            ? table.RowPosY2 - startY : 0f;
    }

    private void RecordNativeRow()
    {
        var table = ImGuiP.GetCurrentTable();
        ImGuiP.TableEndRow(table);
        if (nativeHeights is { } heights && fieldIndex < heights.Length)
            heights[fieldIndex] = table.RowPosY2 - table.RowPosY1;
    }
}
