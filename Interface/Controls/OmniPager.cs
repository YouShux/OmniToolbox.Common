using System.Globalization;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;

namespace OmniToolbox.UI.Controls;

public static class OmniPager
{
    private static readonly List<(int Value, string Label)> PageTokens = new(MAX_PAGE_TOKENS);
    private static (int Page, int Count, string Culture)? TokenKey;
    private static string PageCountText = string.Empty;

    internal static void ClearDisplayCaches()
    {
        PageTokens.Clear();
        TokenKey = null;
        PageCountText = string.Empty;
    }

    public static void Draw
    (
        string  id,
        ref int page,
        ref int jumpPage,
        int     itemCount,
        int     pageSize
    )
    {
        var pageCount = Math.Max(1, (itemCount + pageSize - 1) / pageSize);
        page     = Math.Clamp(page, 1, pageCount);
        jumpPage = Math.Clamp(jumpPage, 1, pageCount);
        ImGui.PushID(id);
        var tokenKey = (page, pageCount, CultureInfo.CurrentCulture.Name);
        if (TokenKey != tokenKey)
        {
            TokenKey = tokenKey;
            PageCountText = pageCount.ToString();
            PageTokens.Clear();
            if (pageCount <= MAX_PAGE_TOKENS)
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

        }
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var jumpLabel = OmniLoc.Get("ItemSearch.Jump");
        var jumpSize = OmniControls.CompactButtonSize(jumpLabel);
        var rowHeight = MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight());
        jumpSize.Y = rowHeight;
        var inputWidth = OmniControls.MeasureInput(PageCountText, OmniTheme.Scale(70f)).X;
        var totalWidth = inputWidth + jumpSize.X + spacing;
        Span<Vector2> tokenSizes = stackalloc Vector2[PageTokens.Count];
        for (var index = 0; index < PageTokens.Count; index++)
        {
            var token = PageTokens[index];
            tokenSizes[index] = token.Value < 0 ? OmniTextMeasurements.Measure(token.Label)
                : OmniControls.CompactButtonSize(token.Label, 0f, 24f) with { Y = rowHeight };
            totalWidth += tokenSizes[index].X;
            totalWidth += spacing;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f));
        for (var i = 0; i < PageTokens.Count; i++)
        {
            var token = PageTokens[i];
            if (i > 0)
            {
                OmniControls.SameLineOrWrap(tokenSizes[i].X);
            }

            if (token.Value < 0)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled(token.Label);
                continue;
            }

            if (OmniControls.SmallButton(
                    token.Label,
                    page == token.Value,
                    tokenSizes[i]))
            {
                page = token.Value;
            }
        }

        OmniControls.SameLineOrWrap(inputWidth + spacing + jumpSize.X);
        using (ImRaii.Group())
        {
            OmniControls.InputInt("##jumpPage", ref jumpPage, inputWidth);
            OmniControls.HelpTooltip(OmniLoc.Get("ItemSearch.Jump.Help"));
            OmniControls.SameLineOrWrap(jumpSize.X);
            if (OmniControls.SmallButton(jumpLabel, false, jumpSize))
            {
                page = Math.Clamp(jumpPage, 1, pageCount);
            }
        }

        ImGui.PopID();

        static void AddPageToken(int value) => PageTokens.Add((value, value.ToString()));
    }

    #region 常量

    private const int MAX_PAGE_TOKENS = 10;

    #endregion
}
