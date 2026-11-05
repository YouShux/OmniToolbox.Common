using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;

namespace OmniToolbox.UI.Controls;

public static partial class OmniControls
{
    internal static Vector2 AttachedTabSize(string label, string? badge)
    {
        var textSize = OmniTextMeasurements.Measure(label);
        if (!string.IsNullOrEmpty(badge))
            textSize.X += ImGui.GetStyle().ItemInnerSpacing.X +
                          OmniTextMeasurements.MeasureAtSize(badge, ImGui.GetFont(), ImGui.GetFontSize() * 0.75f, -1f).X +
                          ImGui.GetStyle().FramePadding.X;
        var size = textSize + ImGui.GetStyle().FramePadding * 2f;
        return new Vector2(MathF.Ceiling(size.X), MathF.Ceiling(size.Y));
    }

    internal static bool AttachedTab(string label, string? badge, bool selected, Vector2 size,
        float separatorLeft, float separatorRight)
    {
        var position = ImGui.GetCursorScreenPos();
        using var colors = ControlScope.PushColor(ImGuiCol.Text, Vector4.Zero)
            .Push(ImGuiCol.Button, Vector4.Zero)
            .Push(ImGuiCol.ButtonHovered, Vector4.Zero)
            .Push(ImGuiCol.ButtonActive, Vector4.Zero)
            .Push(ImGuiCol.Border, Vector4.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f);
        var clicked = ImGui.Button(label, size);
        var hovered = ImGui.IsItemHovered() || ImGui.IsItemFocused();
        var tokens = OmniTheme.Tokens;
        var fill = selected ? tokens.Surface : OmniTheme.HoverBackground;
        var radius = MathF.Min(OmniTheme.Scale(tokens.ButtonRadius), MathF.Min(size.X, size.Y) * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        if (selected || hovered)
            drawList.AddRectFilled(position, position + size, OmniTheme.Color(fill), radius, ImDrawFlags.RoundCornersTop);

        if (selected)
        {
            var thickness = OmniTheme.BorderThickness();
            var inset = new Vector2(thickness * 0.5f);
            var minimum = position + inset;
            var maximum = position + size - inset;
            var borderRadius = MathF.Max(0f, radius - inset.X);
            var baseline = position.Y + size.Y;
            drawList.PathClear();
            drawList.PathLineTo(new Vector2(separatorLeft, baseline));
            drawList.PathLineTo(new Vector2(minimum.X, baseline));
            drawList.PathArcTo(minimum + new Vector2(borderRadius), borderRadius, MathF.PI, MathF.PI * 1.5f);
            drawList.PathArcTo(new Vector2(maximum.X - borderRadius, minimum.Y + borderRadius),
                borderRadius, MathF.PI * 1.5f, MathF.PI * 2f);
            drawList.PathLineTo(new Vector2(maximum.X, baseline));
            drawList.PathLineTo(new Vector2(separatorRight, baseline));
            drawList.PathStroke(OmniTheme.Color(tokens.Border), ImDrawFlags.None, thickness);
        }

        var labelSize = OmniTextMeasurements.Measure(label);
        var badgeSize = string.IsNullOrEmpty(badge)
            ? Vector2.Zero
            : OmniTextMeasurements.MeasureAtSize(badge, ImGui.GetFont(), ImGui.GetFontSize() * 0.75f, -1f);
        var gap = badgeSize.X > 0f ? ImGui.GetStyle().ItemInnerSpacing.X : 0f;
        var badgePadding = badgeSize.X > 0f ? ImGui.GetStyle().FramePadding.X * 0.5f : 0f;
        var width = labelSize.X + gap + badgeSize.X + badgePadding * 2f;
        var textPosition = position + new Vector2((size.X - width) * 0.5f, (size.Y - labelSize.Y) * 0.5f);
        drawList.AddText(textPosition, OmniTheme.Color(tokens.Text), label);
        if (!string.IsNullOrEmpty(badge))
        {
            var badgePosition = position + new Vector2(textPosition.X - position.X + labelSize.X + gap,
                (size.Y - badgeSize.Y) * 0.5f);
            drawList.AddRectFilled(badgePosition - new Vector2(badgePadding, 1f),
                badgePosition + badgeSize + new Vector2(badgePadding, 1f), OmniTheme.Color(tokens.Accent), radius * 0.3f);
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 0.75f, badgePosition,
                OmniTheme.Color(tokens.Text), badge);
        }
        return clicked;
    }

    internal static void DrawAttachedTabContentBackground(Vector2 position, Vector2 size, bool hasTabs)
    {
        var drawList = ImGui.GetWindowDrawList();
        var tokens = OmniTheme.Tokens;
        drawList.AddRectFilled(position, position + size, OmniTheme.Color(tokens.Surface));
        if (!hasTabs)
            drawList.AddLine(position, position + new Vector2(size.X, 0f),
                OmniTheme.Color(tokens.Border), OmniTheme.BorderThickness());
        drawList.AddLine(position + new Vector2(0f, size.Y), position + size,
            OmniTheme.Color(tokens.Border), OmniTheme.BorderThickness());
    }
}
