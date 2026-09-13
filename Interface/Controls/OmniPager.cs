using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public static class OmniPager
{
    private const int MaxPageTokens = 10;
    private static readonly List<(int Value, string Label)> PageTokens = new(MaxPageTokens);

    public static void Draw(string id, ref int page, ref int jumpPage, int itemCount, int pageSize)
    {
        var pageCount = Math.Max(1, (itemCount + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pageCount);
        jumpPage = Math.Clamp(jumpPage, 1, pageCount);
        ImGui.PushID(id);
        PageTokens.Clear();
        if (pageCount <= MaxPageTokens)
        {
            for (var value = 1; value <= pageCount; value++)
            {
                PageTokens.Add((value, value.ToString()));
            }
        }
        else if (page <= 4)
        {
            AddPageToken(1);
            AddPageToken(2);
            AddPageToken(3);
            AddPageToken(4);
            AddPageToken(5);
            PageTokens.Add((-1, "..."));
            AddPageToken(pageCount);
        }
        else if (page >= pageCount - 3)
        {
            AddPageToken(1);
            PageTokens.Add((-1, "..."));
            for (var value = pageCount - 4; value <= pageCount; value++)
            {
                AddPageToken(value);
            }
        }
        else
        {
            AddPageToken(1);
            PageTokens.Add((-1, "..."));
            AddPageToken(page - 1);
            AddPageToken(page);
            AddPageToken(page + 1);
            PageTokens.Add((-1, "..."));
            AddPageToken(pageCount);
        }

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var jumpLabel = OmniLoc.Get("ItemSearch.Jump");
        var jumpSize = OmniControls.CompactButtonSize(jumpLabel);
        var rowHeight = MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight());
        jumpSize.Y = rowHeight;
        var inputWidth = OmniTheme.Scale(70f);
        var totalWidth = inputWidth + jumpSize.X + spacing;
        foreach (var token in PageTokens)
        {
            totalWidth += token.Value < 0
                ? ImGui.CalcTextSize(token.Label).X
                : OmniControls.CompactButtonSize(token.Label, 0f, 24f).X;
            totalWidth += spacing;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f));
        for (var i = 0; i < PageTokens.Count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
            }

            var token = PageTokens[i];
            if (token.Value < 0)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled(token.Label);
                continue;
            }

            if (OmniControls.SmallButton(
                    token.Label,
                    page == token.Value,
                    OmniControls.CompactButtonSize(token.Label, 0f, 24f) with { Y = rowHeight }))
            {
                page = token.Value;
            }
        }

        ImGui.SameLine();
        OmniControls.InputInt("##jumpPage", ref jumpPage, inputWidth);
        OmniControls.HelpTooltip(OmniLoc.Get("ItemSearch.Jump.Help"));
        ImGui.SameLine();
        if (OmniControls.SmallButton(jumpLabel, false, jumpSize))
        {
            page = Math.Clamp(jumpPage, 1, pageCount);
        }

        ImGui.PopID();

        static void AddPageToken(int value) => PageTokens.Add((value, value.ToString()));
    }
}
