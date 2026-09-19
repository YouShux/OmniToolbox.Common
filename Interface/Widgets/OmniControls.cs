using System.Globalization;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Lumina.Text.ReadOnly;
using OmenTools.ImGuiOm;
using OmenTools.OmenService;
using OmniToolbox.Collections;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public static class OmniControls
{
    private const int BORDER_HIGHLIGHT_BANDS = 12;
    private const float BUTTON_HOVER_ALPHA = 0.60f;
    private const float BUTTON_ACTIVE_ALPHA = 0.80f;
    private const float SLIDER_BG_ALPHA = 0.04f;
    private const float SLIDER_HOVER_ALPHA = 0.10f;
    private const float SLIDER_ACTIVE_ALPHA = 0.14f;
    private const float ICON_GLYPH_SCALE = 0.82f;
    private const float TITLE_GLYPH_SCALE = 0.72f;
    private static readonly List<FavoriteParticle> FavoriteParticles = [];
    private static double FavoriteEffectsDrawnAt = -1d;

    public static bool RoundedSelectable(string label, bool selected = false,
        ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, Vector2 size = default)
        => DrawRoundedSelectable(label, selected, flags, size, false);

    public static bool RoundedMenuItem(string label, bool enabled = true)
        => DrawRoundedSelectable(label, false, enabled ? ImGuiSelectableFlags.None : ImGuiSelectableFlags.Disabled, default, true);

    private static bool DrawRoundedSelectable(string label, bool selected, ImGuiSelectableFlags flags, Vector2 size, bool menuItem)
    {
        using var disabled = ImRaii.Disabled((flags & ImGuiSelectableFlags.Disabled) != 0);
        var position = ImGui.GetCursorScreenPos();
        var text = DisplayLabel(label);
        var textSize = ImGui.CalcTextSize(text);
        var rowSize = new Vector2(size.X > 0f ? size.X : MathF.Max(textSize.X, ImGui.GetContentRegionAvail().X),
            size.Y > 0f ? size.Y : MathF.Max(textSize.Y, ImGui.GetTextLineHeight()));
        var textColor = ImGui.GetColorU32(ImGuiCol.Text);
        var selectedColor = ImGui.GetColorU32(ImGuiCol.Header);
        var hoveredColor = ImGui.GetColorU32(ImGuiCol.HeaderHovered);
        var activeColor = ImGui.GetColorU32(ImGuiCol.HeaderActive);
        bool clicked;
        using (ImRaii.PushColor(ImGuiCol.Text, Vector4.Zero)
                   .Push(ImGuiCol.Header, Vector4.Zero)
                   .Push(ImGuiCol.HeaderHovered, Vector4.Zero)
                   .Push(ImGuiCol.HeaderActive, Vector4.Zero))
        {
            // 保留原生选择项的 ID、点击、弹层关闭和导航行为，仅替换绘制。
            clicked = menuItem ? ImGui.MenuItem(label, string.Empty, selected, (flags & ImGuiSelectableFlags.Disabled) == 0)
                : ImGui.Selectable(label, selected, flags, size);
        }
        var drawList = ImGui.GetWindowDrawList();
        if (selected || ImGui.IsItemHovered() || ImGui.IsItemActive())
        {
            drawList.AddRectFilled(position, position + rowSize,
                ImGui.IsItemActive() ? activeColor : ImGui.IsItemHovered() ? hoveredColor : selectedColor,
                MathF.Min(rowSize.Y * 0.5f, OmniTheme.Scale(OmniTheme.Tokens.ButtonRadius)));
        }
        drawList.PushClipRect(position, position + rowSize, true);
        drawList.AddText(position + Vector2.Max(Vector2.Zero, rowSize - textSize) * ImGui.GetStyle().SelectableTextAlign,
            textColor, text);
        drawList.PopClipRect();
        return clicked;
    }

    public static bool CheckedSelectable(string id, string label, bool selected,
        ImGuiSelectableFlags flags = ImGuiSelectableFlags.None)
        => CheckedSelectable(id, label, selected, flags, false);

    public static bool CheckedSelectable(string id, string label, bool selected,
        ImGuiSelectableFlags flags, bool rounded)
    {
        var checkTexture = OmniToolbox.Host.DalamudServices.TextureProvider
            .GetFromGame("ui/uld/ReadyCheck_hr1.tex").GetWrapOrDefault();
        var iconSize = ImGui.GetTextLineHeight();
        var style = ImGui.GetStyle();
        var minimumWidth = iconSize + style.ItemInnerSpacing.X + ImGui.CalcTextSize(label).X + style.FramePadding.X * 2f;
        bool clicked;
        using (ImRaii.PushColor(ImGuiCol.Text, Vector4.Zero))
        {
            var size = new Vector2(MathF.Max(minimumWidth, ImGui.GetContentRegionAvail().X), 0f);
            clicked = rounded ? RoundedSelectable(id, selected, flags, size) : ImGui.Selectable(id, selected, flags, size);
        }
        var position = ImGui.GetItemRectMin() + new Vector2(style.FramePadding.X,
            MathF.Max(0f, (ImGui.GetItemRectSize().Y - iconSize) * 0.5f));
        var drawList = ImGui.GetWindowDrawList();
        if (selected && checkTexture is not null)
        {
            drawList.AddImage(checkTexture.Handle, position, position + new Vector2(iconSize),
                Vector2.Zero, new Vector2(0.5f, 1f));
        }
        drawList.AddText(position + new Vector2(iconSize + style.ItemInnerSpacing.X, 0f),
            OmniTheme.Color(OmniTheme.Tokens.Text), label);
        return clicked;
    }

    public static bool NavButton(string label, bool active, float minimumWidth = 0f)
    {
        var tokens = OmniTheme.Tokens;
        var size = NavButtonSize(label);
        size.X = MathF.Max(size.X, minimumWidth);
        return DrawOutlinedButton(label, size, active ? tokens.Accent : tokens.Surface);
    }

    public static Vector2 NavButtonSize(string label) =>
        new(
            MathF.Max(
                OmniTheme.NavButtonSize().X,
                ImGui.CalcTextSize(DisplayLabel(label)).X + OmniTheme.Scale(24f)),
            MathF.Max(
                OmniTheme.NavButtonSize().Y,
                ImGui.GetTextLineHeightWithSpacing() + OmniTheme.Scale(8f)));

    public static bool SmallButton(string label, bool active) =>
        SmallButton(label, active, CompactButtonSize(label));

    public static bool SmallButton(string label, bool active, Vector2 size)
    {
        var tokens = OmniTheme.Tokens;
        return DrawOutlinedButton(label, size, active ? tokens.Accent : tokens.Surface);
    }

    public static bool SmallButton(string label, bool active, Vector2 size, Vector4 textColor)
    {
        var tokens = OmniTheme.Tokens;
        return DrawOutlinedButton(label, size, active ? tokens.Accent : tokens.Surface, textColor);
    }

    public static bool IconButton(string id, FontAwesomeIcon icon, bool active, string tooltip)
        => IconButton(id, icon, active, new Vector2(OmniTheme.CheckboxSize()), tooltip);

    public static bool IconButton(
        string id,
        FontAwesomeIcon icon,
        bool active,
        Vector2 size,
        string tooltip,
        bool iconVisible = true,
        bool favoriteStyle = false)
    {
        var tokens = OmniTheme.Tokens;
        var isFavorite = icon == FontAwesomeIcon.Heart;
        var useFavoriteStyle = isFavorite || favoriteStyle;
        var pos = ImGui.GetCursorScreenPos();
        var radius = useFavoriteStyle ? OmniTheme.CheckboxRounding() : OmniTheme.Scale(tokens.ButtonRadius);
        var drawList = ImGui.GetWindowDrawList();
        var iconColor = isFavorite
            ? active ? OmniTheme.Favorite : Vector4.One
            : Vector4.One;
        if (OmniTheme.UsesMaterial)
        {
            using var glassColors = ImRaii.PushColor(ImGuiCol.Text, Vector4.Zero)
                .Push(ImGuiCol.Button, Vector4.Zero)
                .Push(ImGuiCol.ButtonHovered, Vector4.Zero)
                .Push(ImGuiCol.ButtonActive, Vector4.Zero)
                .Push(ImGuiCol.Border, Vector4.Zero);
            var clickedGlass = ImGui.Button(id, size);
            var glassPos = pos + new Vector2(0f, GlassMotion.ButtonOffset());
            DrawMaterialControl(drawList, glassPos, size, GlassMotion.ButtonFill(active ? tokens.Accent : tokens.Surface),
                radius, GlassMotion.CurrentItemID);
            if (iconVisible)
            {
                DrawScaledIcon(drawList, icon.ToIconString(), pos + size * 0.5f,
                    isFavorite && active ? OmniTheme.Favorite : tokens.Text);
            }
            HelpTooltip(tooltip);
            return clickedGlass;
        }
        DrawControlShadow(drawList, pos, size, radius);
        using var textColor = ImRaii.PushColor(ImGuiCol.Text, Vector4.Zero);
        using var colors = ImRaii.PushColor(ImGuiCol.Button, active && !useFavoriteStyle ? tokens.Accent : tokens.Surface)
            .Push(ImGuiCol.ButtonHovered, OmniTheme.UsesDarkPalette ? OmniTheme.HoverBackground : tokens.Accent with { W = BUTTON_HOVER_ALPHA })
            .Push(ImGuiCol.ButtonActive, OmniTheme.UsesDarkPalette ? OmniTheme.ActiveBackground : tokens.Accent with { W = BUTTON_ACTIVE_ALPHA })
            .Push(ImGuiCol.Border, tokens.Border);
        using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(ImGuiStyleVar.FrameRounding, radius);
        var clicked = ImGuiOm.ButtonIcon(id, icon, size, string.Empty, useStaticFont: false);

        DrawControlFrame(
            drawList,
            pos,
            size,
            radius,
            useFavoriteStyle ? OmniTheme.CheckboxBorderThickness(active) : OmniTheme.BorderThickness());
        DrawControlHighlight(drawList, pos, size, radius);
        if (iconVisible)
        {
            var iconText = icon.ToIconString();
            var glyphScale = ICON_GLYPH_SCALE;
            if (favoriteStyle && !isFavorite)
            {
                var iconSize = GetScaledIconSize(iconText, 1f);
                var favoriteSize = GetScaledIconSize(FontAwesomeIcon.Heart.ToIconString(), 1f);
                glyphScale *= MathF.Min(
                    favoriteSize.X / MathF.Max(1f, iconSize.X),
                    favoriteSize.Y / MathF.Max(1f, iconSize.Y));
            }

            DrawScaledIcon(
                drawList,
                iconText,
                pos + size * 0.5f + (isFavorite ? OmniTheme.Scale(new Vector2(0f, 2f)) : Vector2.Zero),
                iconColor,
                horizontalOffset: favoriteStyle && !isFavorite ? 1f : 0f,
                glyphScale: glyphScale);
        }

        HelpTooltip(tooltip);
        if (clicked && isFavorite && !active)
        {
            FavoriteBurst(pos + new Vector2(size.X * 0.5f, 0f));
        }

        DrawFavoriteEffects();

        return clicked;
    }

    public static bool CloseButton(string id, Vector2 size)
    {
        var position = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(id, size);
        DrawTitleGlyph(
            FontAwesomeIcon.Times,
            position + size * 0.5f,
            MathF.Min(size.X, size.Y) * TITLE_GLYPH_SCALE);
        HelpTooltip(OmniLoc.Get("Common.Close"));
        return ImGui.IsItemClicked();
    }

    public static bool TitleTriangleButton(bool collapsed, string id)
    {
        var size = OmniTheme.TitleIconSize();
        var position = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(id, size);
        DrawTitleGlyph(
            collapsed ? FontAwesomeIcon.CaretRight : FontAwesomeIcon.CaretDown,
            position + size * 0.5f,
            MathF.Min(size.X, size.Y) * TITLE_GLYPH_SCALE);
        HelpTooltip(OmniLoc.Get("Tooltip.ToggleCollapse"));
        return ImGui.IsItemClicked();
    }

    public static Vector2 FavoriteButtonSize() => new(OmniTheme.CheckboxSize());

    public static bool FavoriteButton(string id, bool favorite, string tooltip) =>
        IconButton(id, FontAwesomeIcon.Heart, favorite, FavoriteButtonSize(), tooltip);

    public static void FavoriteBurst(Vector2 origin)
    {
        var startedAt = ImGui.GetTime();
        for (var index = 0; index < 20; index++)
        {
            FavoriteParticles.Add(new(
                origin + OmniTheme.Scale(new Vector2((Random.Shared.NextSingle() - 0.5f) * 8f, -2f)),
                OmniTheme.Scale(new Vector2(
                    (Random.Shared.NextSingle() - 0.5f) * 130f,
                    -90f - Random.Shared.NextSingle() * 95f)),
                0.52f + Random.Shared.NextSingle() * 0.48f,
                1.10f + Random.Shared.NextSingle() * 0.45f,
                startedAt));
        }
    }

    public static void DrawFavoriteEffects()
    {
        if (FavoriteParticles.Count == 0)
        {
            return;
        }

        var now = ImGui.GetTime();
        if (FavoriteEffectsDrawnAt == now)
        {
            return;
        }

        FavoriteEffectsDrawnAt = now;
        var icon = FontAwesomeIcon.Heart.ToIconString();
        var drawList = ImGui.GetForegroundDrawList();
        for (var index = FavoriteParticles.Count - 1; index >= 0; index--)
        {
            var particle = FavoriteParticles[index];
            var elapsed = (float)(now - particle.StartedAt);
            if (elapsed >= particle.Lifetime)
            {
                FavoriteParticles.RemoveAt(index);
                continue;
            }

            var progress = elapsed / particle.Lifetime;
            var scale = particle.Scale * (1f - progress * 0.18f);
            var iconSize = GetScaledIconSize(icon) * scale;
            var position = particle.Origin + particle.Velocity * elapsed +
                           new Vector2(0f, OmniTheme.Scale(260f) * elapsed * elapsed * 0.5f);
            drawList.AddText(
                ImGui.GetFont(),
                ImGui.GetFontSize() * ICON_GLYPH_SCALE * scale,
                position - iconSize * 0.5f,
                OmniTheme.Color(OmniTheme.Favorite with { W = 1f - progress }),
                icon);
        }
    }

    public static bool Checkbox(string label, ref bool value) =>
        Checkbox(label, ref value, OmniTheme.CheckboxSize());

    public static bool Checkbox(string label, ref bool value, float controlSize)
    {
        var tokens = OmniTheme.Tokens;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Max(ImGui.GetTextLineHeight(), controlSize));
        var radius = OmniTheme.CheckboxRounding();
        if (OmniTheme.UsesMaterial)
        {
            var id = ImGui.GetID(label);
            var hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + size, true);
            DrawMaterialControl(ImGui.GetWindowDrawList(), pos, size,
                GlassMotion.ControlFill(id, value ? tokens.Accent : tokens.Surface, hovered, GlassMotion.ActiveItemID == id),
                OmniTheme.Scale(tokens.ButtonRadius), id);
        }
        else
        {
            DrawControlShadow(ImGui.GetWindowDrawList(), pos, size, radius);
        }
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.FrameBg, OmniTheme.UsesMaterial ? Vector4.Zero : tokens.Border with { W = 0.06f })
            .Push(ImGuiCol.FrameBgHovered, OmniTheme.UsesMaterial ? Vector4.Zero : tokens.Border with { W = 0.12f })
            .Push(ImGuiCol.FrameBgActive, OmniTheme.UsesMaterial ? Vector4.Zero : tokens.Border with { W = 0.16f })
            .Push(ImGuiCol.CheckMark, tokens.Text);
        var changed = ImGui.Checkbox(label, ref value);
        if (!OmniTheme.UsesMaterial)
        {
            DrawControlHighlight(ImGui.GetWindowDrawList(), pos, size, radius);
            DrawControlFrame(
                ImGui.GetWindowDrawList(),
                pos,
                size,
                radius,
                OmniTheme.CheckboxBorderThickness(value));
        }
        return changed;
    }

    public static bool RadioButton(string label, bool active)
    {
        var tokens = OmniTheme.Tokens;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Max(ImGui.GetTextLineHeight(), OmniTheme.CheckboxSize()));
        var radius = size.X * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        if (OmniTheme.UsesMaterial)
        {
            var id = ImGui.GetID(label);
            var hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + size, true);
            DrawMaterialControl(drawList, pos, size,
                GlassMotion.ControlFill(id, active ? tokens.Accent : tokens.Surface, hovered, GlassMotion.ActiveItemID == id),
                radius, id);
        }
        else
        {
            DrawControlShadow(drawList, pos, size, radius);
        }

        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.FrameBg, OmniTheme.UsesMaterial ? Vector4.Zero : tokens.Border with { W = 0.06f })
            .Push(ImGuiCol.FrameBgHovered, OmniTheme.UsesMaterial ? Vector4.Zero : tokens.Border with { W = 0.12f })
            .Push(ImGuiCol.FrameBgActive, OmniTheme.UsesMaterial ? Vector4.Zero : tokens.Border with { W = 0.16f })
            .Push(ImGuiCol.CheckMark, tokens.Text);
        var clicked = ImGui.RadioButton(label, active);
        if (!OmniTheme.UsesMaterial)
        {
            DrawControlHighlight(drawList, pos, size, radius);
            DrawControlFrame(drawList, pos, size, radius, OmniTheme.CheckboxBorderThickness(active));
        }
        return clicked;
    }

    public static bool ColorEdit(string id, ref Vector3 color)
        => ColorEdit(id, ref color, ImGuiColorEditFlags.None);

    public static bool ColorEdit(string id, ref Vector3 color, ImGuiColorEditFlags displayFlags)
    {
        var width = OmniTheme.CheckboxSize();
        id = DrawLeadingControlLabel(id, ref width);
        ImGui.SetNextItemWidth(width);
        return ImGui.ColorEdit3(
            id,
            ref color,
            ImGuiColorEditFlags.NoInputs |
            ImGuiColorEditFlags.PickerHueBar |
            displayFlags);
    }

    public static bool ColorEdit(string id, ref Vector4 color)
    {
        var width = OmniTheme.CheckboxSize();
        id = DrawLeadingControlLabel(id, ref width);
        ImGui.SetNextItemWidth(width);
        return ImGui.ColorEdit4(
            id,
            ref color,
            ImGuiColorEditFlags.NoInputs |
            ImGuiColorEditFlags.PickerHueBar |
            ImGuiColorEditFlags.AlphaBar);
    }

    public static bool InputTextWithHint(string id, string hint, ref string value, int maxLength) =>
        InputTextWithHint(id, hint, ref value, maxLength, ImGui.CalcItemWidth());

    public static bool InputTextWithHint(string id, string hint, ref string value, int maxLength, float width)
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, true);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.TextDisabled, tokens.Text with { W = 0.70f })
            .Push(ImGuiCol.FrameBg, Vector4.Zero)
            .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
            .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
            .Push(ImGuiCol.Border, Vector4.Zero);
        var changed = ImGui.InputTextWithHint(id, hint, ref value, maxLength);
        EndNativeControl(frame);
        return changed;
    }

    public static bool InputText(
        string id,
        ref string value,
        int maxLength,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None) =>
        InputText(id, ref value, maxLength, ImGui.CalcItemWidth(), flags);

    public static bool InputText(
        string id,
        ref string value,
        int maxLength,
        float width,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, true);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.FrameBg, Vector4.Zero)
            .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
            .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
            .Push(ImGuiCol.Border, Vector4.Zero);
        var changed = ImGui.InputText(id, ref value, maxLength, flags);
        EndNativeControl(frame);
        return changed;
    }

    public static bool InputTextMultiline(
        string id,
        ref string value,
        int maxLength,
        Vector2 size,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        var tokens = OmniTheme.Tokens;
        var width = size.X;
        id = DrawLeadingControlLabel(id, ref width);
        size.X = width;
        var frame = BeginNativeControl(id, size, true);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f);
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.FrameBg, Vector4.Zero)
            .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
            .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
            .Push(ImGuiCol.Border, Vector4.Zero);
        var changed = ImGui.InputTextMultiline(id, ref value, maxLength, frame.Size, flags);
        EndNativeControl(frame);
        return changed;
    }

    public static bool InputInt(string id, ref int value, int step = 0, int stepFast = 0) =>
        InputInt(id, ref value, ImGui.CalcItemWidth(), step, stepFast);

    public static bool InputInt(
        string id,
        ref int value,
        float width,
        int step = 0,
        int stepFast = 0,
        bool groupThousands = false)
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, true);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.FrameBg, Vector4.Zero)
            .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
            .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
            .Push(ImGuiCol.Border, Vector4.Zero);
        var changed = ImGui.InputInt(id, ref value, step, stepFast);
        if (groupThousands && step == 0 && !ImGui.IsItemActive())
        {
            var drawList = ImGui.GetWindowDrawList();
            var inset = MathF.Max(1f, OmniTheme.BorderThickness());
            drawList.PushClipRect(frame.Pos + new Vector2(inset), frame.Pos + frame.Size - new Vector2(inset), true);
            drawList.AddRectFilled(
                frame.Pos + new Vector2(inset),
                frame.Pos + frame.Size - new Vector2(inset),
                OmniTheme.Color(tokens.Surface with { W = 1f }),
                MathF.Max(0f, frame.Radius - inset));
            drawList.AddText(
                frame.Pos + new Vector2(
                    ImGui.GetStyle().FramePadding.X,
                    MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)),
                ImGui.GetColorU32(ImGuiCol.Text),
                value.ToString("N0", CultureInfo.InvariantCulture));
            drawList.PopClipRect();
        }

        EndNativeControl(frame);
        return changed;
    }

    public static bool InputFloat(
        string id,
        ref float value,
        float step = 0f,
        float stepFast = 0f,
        string format = "%.3f") =>
        InputFloat(id, ref value, ImGui.CalcItemWidth(), step, stepFast, format);

    public static bool InputFloat(
        string id,
        ref float value,
        float width,
        float step = 0f,
        float stepFast = 0f,
        string format = "%.3f")
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, true);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.FrameBg, Vector4.Zero)
            .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
            .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
            .Push(ImGuiCol.Border, Vector4.Zero);
        var changed = ImGui.InputFloat(id, ref value, step, stepFast, format);
        EndNativeControl(frame);
        return changed;
    }

    public static bool SteppedInputInt(
        string id,
        ref int value,
        float inputWidth,
        int minimum = 0,
        int maximum = int.MaxValue,
        int step = 1,
        int stepFast = 0,
        bool groupThousands = false)
    {
        var changed = InputInt(id, ref value, inputWidth, groupThousands: groupThousands);
        if (changed)
        {
            value = Math.Clamp(value, minimum, maximum);
        }

        changed |= ImGui.IsItemDeactivatedAfterEdit();
        var buttonSize = new Vector2(OmniTheme.SmallButtonSize().Y);
        var spacing = OmniTheme.Scale(6f);
        ImGui.SameLine(0f, spacing);
        var amount = ImGui.GetIO().KeyCtrl && stepFast > 0 ? stepFast : Math.Max(1, step);
        if (StepButton($"{id}Decrease", false, buttonSize))
        {
            value = (int)Math.Clamp((long)value - amount, minimum, maximum);
            changed = true;
        }

        ImGui.SameLine(0f, spacing);
        if (StepButton($"{id}Increase", true, buttonSize))
        {
            value = (int)Math.Clamp((long)value + amount, minimum, maximum);
            changed = true;
        }

        return changed;
    }

    public static bool InputIntRange(
        string id,
        ref int minimum,
        ref int maximum,
        int lowerBound,
        int upperBound)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var separatorWidth = OmniTheme.Scale(24f);
        var inputWidth = MathF.Max(
            OmniTheme.Scale(64f),
            (ImGui.GetContentRegionAvail().X - separatorWidth - spacing * 2f) * 0.5f);
        InputInt($"{id}Min", ref minimum, inputWidth);
        var changed = ImGui.IsItemDeactivatedAfterEdit();
        ImGui.SameLine(0f, spacing);
        var separatorPosition = ImGui.GetCursorScreenPos();
        var separatorSize = new Vector2(separatorWidth, MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight()));
        ImGui.InvisibleButton($"{id}Separator", separatorSize);
        ImGui.GetWindowDrawList().AddLine(
            separatorPosition + new Vector2(OmniTheme.Scale(5f), separatorSize.Y * 0.5f),
            separatorPosition + new Vector2(separatorSize.X - OmniTheme.Scale(5f), separatorSize.Y * 0.5f),
            OmniTheme.Color(OmniTheme.Tokens.Text),
            OmniTheme.BorderThickness());
        ImGui.SameLine(0f, spacing);
        InputInt($"{id}Max", ref maximum, inputWidth);
        changed |= ImGui.IsItemDeactivatedAfterEdit();
        if (!changed)
        {
            return false;
        }

        minimum = Math.Clamp(minimum, lowerBound, upperBound);
        maximum = Math.Clamp(maximum, lowerBound, upperBound);
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        return true;
    }

    public static bool SliderFloat(string id, ref float value, float min, float max, string format, float width)
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, false);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.GrabRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.FrameBg, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_BG_ALPHA })
            .Push(ImGuiCol.FrameBgHovered, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_HOVER_ALPHA })
            .Push(ImGuiCol.FrameBgActive, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_ACTIVE_ALPHA })
            .Push(ImGuiCol.SliderGrab, OmniTheme.ControlAccent)
            .Push(ImGuiCol.SliderGrabActive, OmniTheme.UsesMaterial ? OmniTheme.ControlAccent : tokens.Secondary)
            .Push(ImGuiCol.Text, tokens.Text);
        var changed = ImGui.SliderFloat(id, ref value, min, max, format);
        EndNativeControl(frame);
        return changed;
    }

    public static bool SliderInt(string id, ref int value, int min, int max, string format, float width)
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, false);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.GrabRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.FrameBg, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_BG_ALPHA })
            .Push(ImGuiCol.FrameBgHovered, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_HOVER_ALPHA })
            .Push(ImGuiCol.FrameBgActive, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_ACTIVE_ALPHA })
            .Push(ImGuiCol.SliderGrab, OmniTheme.ControlAccent)
            .Push(ImGuiCol.SliderGrabActive, OmniTheme.UsesMaterial ? OmniTheme.ControlAccent : tokens.Secondary)
            .Push(ImGuiCol.Text, tokens.Text);
        var changed = ImGui.SliderInt(id, ref value, min, max, format);
        EndNativeControl(frame);
        return changed;
    }

    public static bool DragFloat(
        string id,
        ref float value,
        float speed,
        float min,
        float max,
        string format,
        float width,
        ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, false);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.FrameBg, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_BG_ALPHA })
            .Push(ImGuiCol.FrameBgHovered, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_HOVER_ALPHA })
            .Push(ImGuiCol.FrameBgActive, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_ACTIVE_ALPHA })
            .Push(ImGuiCol.Text, tokens.Text);
        var changed = ImGui.DragFloat(id, ref value, speed, min, max, format, flags);
        EndNativeControl(frame);
        return changed;
    }

    public static bool DragFloat2(
        string id,
        ref Vector2 value,
        float speed,
        float min,
        float max,
        string format,
        float width)
    {
        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, false);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.FrameBg, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_BG_ALPHA })
            .Push(ImGuiCol.FrameBgHovered, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_HOVER_ALPHA })
            .Push(ImGuiCol.FrameBgActive, OmniTheme.UsesMaterial ? Vector4.Zero : Vector4.One with { W = SLIDER_ACTIVE_ALPHA })
            .Push(ImGuiCol.Text, tokens.Text);
        var changed = ImGui.DragFloat2(id, ref value, speed, min, max, format);
        EndNativeControl(frame);
        return changed;
    }

    public static bool BeginCombo(
        string id,
        string preview,
        float width,
        ImGuiComboFlags flags = ImGuiComboFlags.None)
    {
        const ImGuiComboFlags HEIGHT_MASK = ImGuiComboFlags.HeightSmall |
                                           ImGuiComboFlags.HeightRegular |
                                           ImGuiComboFlags.HeightLarge |
                                           ImGuiComboFlags.HeightLargest;
        if ((flags & HEIGHT_MASK) == ImGuiComboFlags.None ||
            (flags & ImGuiComboFlags.HeightLargest) != ImGuiComboFlags.None)
        {
            flags = flags & ~HEIGHT_MASK | ImGuiComboFlags.HeightLarge;
        }

        var tokens = OmniTheme.Tokens;
        id = DrawLeadingControlLabel(id, ref width);
        var frame = BeginNativeControl(id, width, true);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, frame.Radius)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(
                ImGuiStyleVar.FramePadding,
                new Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (frame.Size.Y - ImGui.GetTextLineHeight()) * 0.5f)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, tokens.Text)
            .Push(ImGuiCol.FrameBg, Vector4.Zero)
            .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
            .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
            .Push(ImGuiCol.Button, Vector4.Zero)
            .Push(ImGuiCol.ButtonHovered, Vector4.Zero)
            .Push(ImGuiCol.ButtonActive, Vector4.Zero)
            .Push(ImGuiCol.Border, OmniTheme.UsesMaterial ? tokens.Border : Vector4.Zero)
            .Push(ImGuiCol.BorderShadow, Vector4.Zero);
        var open = ImGui.BeginCombo(id, preview, flags);
        EndNativeControl(frame, open);
        return open;
    }

    public static Vector2 CompactButtonSize(string label, float minimumWidth = 64f, float horizontalPadding = 16f) =>
        new(
            MathF.Max(OmniTheme.Scale(minimumWidth), ImGui.CalcTextSize(DisplayLabel(label)).X + OmniTheme.Scale(horizontalPadding)),
            MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight()));

    public static bool StepButton(string id, bool plus, Vector2 size)
    {
        var clicked = SmallButton($"##{id}", false, size);
        var center = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
        var half = OmniTheme.Scale(6.5f);
        var thickness = OmniTheme.Scale(2.4f);
        var color = OmniTheme.Color(OmniTheme.Tokens.Text);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(
            new Vector2(center.X - half, center.Y),
            new Vector2(center.X + half, center.Y),
            color,
            thickness);
        if (plus)
        {
            drawList.AddLine(
                new Vector2(center.X, center.Y - half),
                new Vector2(center.X, center.Y + half),
                color,
                thickness);
        }

        return clicked;
    }

    public static bool CollapsingHeader(
        string label,
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        var tokens = OmniTheme.Tokens;
        var rightInset = OmniTheme.BorderThickness() + OmniTheme.Scale(MathF.Max(0f, tokens.ShadowOffset));
        var frame = BeginNativeControl(
            label,
            new Vector2(MathF.Max(1f, ImGui.GetContentRegionAvail().X - rightInset), ImGui.GetFrameHeight()),
            false);
        var hovered = ImGui.IsMouseHoveringRect(frame.Pos, frame.Pos + frame.Size, true);
        var fill = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left)
            ? tokens.Accent with { W = BUTTON_ACTIVE_ALPHA }
            : hovered
                ? tokens.Accent with { W = BUTTON_HOVER_ALPHA }
                : tokens.Primary;
        if (!OmniTheme.UsesMaterial)
        {
            frame.DrawList.AddRectFilled(
                frame.Pos,
                frame.Pos + frame.Size,
                OmniTheme.Color(fill),
                frame.Radius,
                ImDrawFlags.RoundCornersAll);
        }
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(ImGuiStyleVar.FrameRounding, frame.Radius);
        using var colors = ImRaii.PushColor(ImGuiCol.Header, Vector4.Zero)
            .Push(ImGuiCol.HeaderHovered, Vector4.Zero)
            .Push(ImGuiCol.HeaderActive, Vector4.Zero);
        var open = ImGui.CollapsingHeader(label, flags | ImGuiTreeNodeFlags.SpanAvailWidth);
        EndNativeControl(frame);
        return open;
    }

    public static void SectionLabel(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, OmniTheme.Tokens.Text);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
    }

    public static void PreviewImageIcon(string url)
    {
        var lineTop = ImGui.GetItemRectMin().Y;
        var lineHeight = ImGui.GetItemRectSize().Y;
        ImGui.SameLine(0f, OmniTheme.Scale(5f));
        DrawInlineIcon(
            FontAwesomeIcon.Image,
            lineTop,
            lineHeight,
            OmniTheme.Tokens.Text with { W = 0.70f },
            OmniTheme.Scale(1.5f));
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            return;
        }

        using var tooltipFont = BeginHelpTooltip();
        if (!ImageHelper.Instance().TryGetImage(url, out var texture))
        {
            ImGui.TextDisabled($"{OmniLoc.Get("ItemSearch.Price.Loading")}...");
            EndHelpTooltip();
            return;
        }

        var availableSize = ImGui.GetMainViewport().WorkSize - OmniTheme.Scale(new Vector2(48f));
        var scale = MathF.Min(
            1f,
            MathF.Min(
                MathF.Min(OmniTheme.Scale(720f), availableSize.X) / texture.Size.X,
                MathF.Min(OmniTheme.Scale(540f), availableSize.Y) / texture.Size.Y));
        ImGui.Image(texture.Handle, texture.Size * MathF.Max(0.01f, scale));
        EndHelpTooltip();
    }

    public static void DrawWindowBackground(Vector2 pos, Vector2 size, bool collapsed)
    {
        if (size.X <= 1f || size.Y <= 1f)
        {
            return;
        }

        var tokens = OmniTheme.Tokens;
        var drawList = ImGui.GetWindowDrawList();
        if (OmniTheme.UsesMaterial)
        {
            var glassRadius = OmniTheme.Scale(tokens.BorderRadius);
            var outerGlow = OmniTheme.Scale(new Vector2(OmniTheme.IsGlass ? 8f : 18f));
            drawList.PushClipRect(pos - outerGlow, pos + size + outerGlow, false);
            DrawGlassSurface(drawList, pos, size, tokens.Background, glassRadius);
            if (!collapsed)
            {
                var y = pos.Y + OmniTheme.TitleBarHeight();
                drawList.AddLine(new Vector2(pos.X + glassRadius, y),
                    new Vector2(pos.X + size.X - glassRadius, y),
                    OmniTheme.Color(tokens.Border), OmniTheme.BorderThickness());
            }
            drawList.PopClipRect();
            return;
        }
        var border = OmniTheme.MainPanelBorderThickness();
        var radius = OmniTheme.Scale(tokens.BorderRadius);
        var shadowOffset = OmniTheme.Scale(MathF.Max(0f, tokens.ShadowOffset));
        drawList.PushClipRect(
            pos - new Vector2(border),
            pos + size + new Vector2(border + shadowOffset),
            false);
        DrawControlShadow(drawList, pos, size, radius);
        drawList.AddRectFilled(
            pos,
            pos + size,
            OmniTheme.Color(collapsed ? tokens.Primary with { W = 1f } : tokens.Background),
            radius);
        if (!collapsed)
        {
            drawList.AddRectFilled(
                pos,
                pos + new Vector2(size.X, OmniTheme.TitleBarHeight()),
                OmniTheme.Color(tokens.Primary),
                radius,
                ImDrawFlags.RoundCornersTop);
            drawList.AddLine(
                pos + new Vector2(border * 0.5f, OmniTheme.TitleBarHeight()),
                pos + new Vector2(size.X - border * 0.5f, OmniTheme.TitleBarHeight()),
                OmniTheme.Color(tokens.Border),
                border);
        }

        DrawControlFrame(drawList, pos, size, radius, border);
        DrawControlHighlight(drawList, pos, size, radius);
        drawList.PopClipRect();
    }

    public static void DrawTextCentered(string text, Vector2 pos, Vector2 size, Vector4 color)
    {
        var textSize = ImGui.CalcTextSize(text);
        ImGui.GetWindowDrawList().AddText(
            pos + new Vector2(MathF.Max(0f, (size.X - textSize.X) * 0.5f), MathF.Max(0f, (size.Y - textSize.Y) * 0.5f)),
            OmniTheme.Color(color),
            text);
    }

    public static void BeginTableHeaderRow() =>
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, OmniTheme.SmallButtonSize().Y);

    public static void BeginTableHeaderRow(float contentHeight) =>
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, contentHeight);

    public static void ScrollableTableHeadersRow(int frozenColumns = 0)
    {
        ImGui.TableSetupScrollFreeze(frozenColumns, 1);
        ImGui.TableHeadersRow();
    }

    public static void TableHeader(string label, string? tooltip = null)
    {
        ImGui.TableNextColumn();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));
        if (tooltip is null)
        {
            TableTextCentered(label);
            return;
        }

        var icon = FontAwesomeIcon.InfoCircle.ToIconString();
        var spacing = OmniTheme.Scale(4f);
        var iconSize = GetScaledIconSize(icon);
        var textSize = ImGui.CalcTextSize(label);
        var width = textSize.X + spacing + iconSize.X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - width) * 0.5f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        var lineTop = ImGui.GetItemRectMin().Y;
        var lineHeight = ImGui.GetItemRectSize().Y;
        ImGui.SameLine(0f, spacing);
        DrawHelpIcon(lineTop, lineHeight, OmniTheme.Scale(1.5f));
        HelpTooltip(tooltip);
    }

    public static void TableHeader(string label, float contentHeight)
    {
        ImGui.TableNextColumn();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));
        TableTextCentered(label, contentHeight);
    }

    public static void AvailabilityTableHeader(string label, string introduction)
    {
        ImGui.TableNextColumn();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));
        var icon = FontAwesomeIcon.InfoCircle.ToIconString();
        var spacing = OmniTheme.Scale(4f);
        var iconSize = GetScaledIconSize(icon);
        var textSize = ImGui.CalcTextSize(label);
        var width = textSize.X + spacing + iconSize.X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - width) * 0.5f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        var lineTop = ImGui.GetItemRectMin().Y;
        var lineHeight = ImGui.GetItemRectSize().Y;
        ImGui.SameLine(0f, spacing);
        DrawHelpIcon(lineTop, lineHeight, OmniTheme.Scale(1.5f));
        DrawAvailabilityHelpTooltip(introduction);
    }

    public static Vector2 HelpIconSize() =>
        new(
            GetScaledIconSize(FontAwesomeIcon.InfoCircle.ToIconString()).X,
            OmniTheme.SmallButtonSize().Y);

    public static void HelpIcon(string tooltip)
    {
        DrawHelpIcon(
            ImGui.GetItemRectMin().Y,
            ImGui.GetItemRectSize().Y,
            OmniTheme.Scale(1.5f));
        HelpTooltip(tooltip);
    }

    public static void HelpIcon(Action drawTooltip)
        => HelpIcon(drawTooltip, OmniTheme.Scale(1.5f));

    public static void HelpIcon(Action drawTooltip, float verticalOffset)
    {
        DrawHelpIcon(
            ImGui.GetItemRectMin().Y,
            ImGui.GetItemRectSize().Y,
            verticalOffset);
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            return;
        }

        using var tooltipFont = BeginHelpTooltip();
        drawTooltip();
        EndHelpTooltip();
    }

    public static void HelpIcon(string tooltip, string markerText, Vector4 markerColor)
    {
        DrawHelpIcon(
            ImGui.GetItemRectMin().Y,
            ImGui.GetItemRectSize().Y,
            OmniTheme.Scale(1.5f));
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) || string.IsNullOrWhiteSpace(tooltip))
        {
            return;
        }

        using var tooltipFont = BeginHelpTooltip();
        foreach (var line in tooltip.Split('\n'))
        {
            HelpTooltipLine(line, markerText, markerColor);
        }

        EndHelpTooltip();
    }

    public static void AvailabilityHelpIcon()
    {
        DrawHelpIcon(ImGui.GetItemRectMin().Y, ImGui.GetItemRectSize().Y);
        DrawAvailabilityHelpTooltip();
    }

    public static void TableHeaderWithStatus(string label, string status, Vector4 statusColor)
    {
        ImGui.TableNextColumn();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));
        var statusText = $"[{status}]";
        var spacing = OmniTheme.Scale(4f);
        var width = ImGui.CalcTextSize(label).X + spacing + ImGui.CalcTextSize(statusText).X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - width) * 0.5f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(0f, spacing);
        ImGui.TextColored(statusColor, statusText);
    }

    public static void InlineStatusLabel(string label, Vector4 color, bool alignToFramePadding = true)
    {
        ImGui.SameLine(0f, OmniTheme.Scale(4f));
        if (alignToFramePadding)
        {
            ImGui.AlignTextToFramePadding();
        }

        ImGui.TextColored(color, $"[{label}]");
    }

    public static void InlineAutoPathStatus(bool enabled, bool alignToFramePadding = true) =>
        InlineStatusLabel(
            OmniLoc.Get(enabled ? "Common.Status.AutoPathEnabled" : "Common.Status.AutoPathDisabled"),
            enabled ? OmniTheme.Tokens.Success : OmniTheme.Tokens.Error,
            alignToFramePadding);

    public static void CenterTableItem(Vector2 size, float contentHeight = 0f)
    {
        var cursor = ImGui.GetCursorPos();
        var availableWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(cursor.X + MathF.Max(0f, (availableWidth - size.X) * 0.5f));
        if (contentHeight > size.Y)
        {
            ImGui.SetCursorPosY(cursor.Y + (contentHeight - size.Y) * 0.5f);
        }
    }

    public static void TableTextCentered(string text, float contentHeight = 0f, Vector4? color = null)
    {
        if (contentHeight > 0f)
        {
            DrawTextCentered(
                text,
                ImGui.GetCursorScreenPos(),
                new Vector2(ImGui.GetContentRegionAvail().X, contentHeight),
                color ?? ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
            ImGui.Dummy(new Vector2(0f, contentHeight));
            return;
        }

        CenterTableItem(ImGui.CalcTextSize(text), contentHeight);
        ImGui.AlignTextToFramePadding();

        if (color is { } textColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();
            return;
        }

        ImGui.TextUnformatted(text);
    }

    public static void TableTextWrappedCentered(string text)
    {
        var cursorX = ImGui.GetCursorPosX();
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var textSize = ImGui.CalcTextSize(text, false, availableWidth);
        ImGui.SetCursorPosX(cursorX + MathF.Max(0f, (availableWidth - textSize.X) * 0.5f));
        ImGui.PushTextWrapPos(cursorX + availableWidth);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
    }

    private static bool DrawOutlinedButton(
        string label,
        Vector2 size,
        Vector4 normal,
        Vector4? textColor = null)
    {
        var pos = ImGui.GetCursorScreenPos();
        var clicked = false;
        if (OmniTheme.UsesMaterial)
        {
            using var colors = ImRaii.PushColor(ImGuiCol.Text, Vector4.Zero)
                .Push(ImGuiCol.Button, Vector4.Zero)
                .Push(ImGuiCol.ButtonHovered, Vector4.Zero)
                .Push(ImGuiCol.ButtonActive, Vector4.Zero)
                .Push(ImGuiCol.Border, Vector4.Zero);
            clicked = ImGui.Button(label, size);
        }
        else
        {
            ImGui.InvisibleButton(label, size);
        }
        var tokens = OmniTheme.Tokens;
        var fill = ImGui.IsItemActive()
            ? OmniTheme.UsesDarkPalette ? OmniTheme.ActiveBackground : tokens.Accent with { W = BUTTON_ACTIVE_ALPHA }
            : ImGui.IsItemHovered()
                ? OmniTheme.UsesDarkPalette ? OmniTheme.HoverBackground : tokens.Accent with { W = BUTTON_HOVER_ALPHA }
                : normal;
        var radius = OmniTheme.Scale(tokens.ButtonRadius);
        var drawList = ImGui.GetWindowDrawList();
        if (OmniTheme.UsesMaterial)
        {
            DrawMaterialControl(drawList, pos + new Vector2(0f, GlassMotion.ButtonOffset()), size,
                GlassMotion.ButtonFill(normal), radius, GlassMotion.CurrentItemID);
            DrawTextCentered(DisplayLabel(label), pos, size, textColor ?? tokens.Text);
            return clicked;
        }
        DrawControlShadow(drawList, pos, size, radius);
        drawList.AddRectFilled(pos, pos + size, OmniTheme.Color(fill), radius);
        DrawControlFrame(drawList, pos, size, radius, OmniTheme.BorderThickness());
        DrawControlHighlight(drawList, pos, size, radius);
        DrawTextCentered(DisplayLabel(label), pos, size, textColor ?? tokens.Text);
        return ImGui.IsItemClicked();
    }

    internal static void DrawPanelBackground(Vector2 pos, Vector2 size, Vector4 fill)
    {
        if (size.X <= 1f || size.Y <= 1f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var radius = OmniTheme.Scale(OmniTheme.Tokens.BorderRadius);
        if (OmniTheme.UsesMaterial)
        {
            drawList.AddRectFilled(pos, pos + size, OmniTheme.Color(fill with { W = fill.W * 0.30f }), 0f);
            return;
        }
        DrawControlShadow(drawList, pos, size, radius);
        drawList.AddRectFilled(pos, pos + size, OmniTheme.Color(fill), radius);
        DrawControlFrame(drawList, pos, size, radius, OmniTheme.BorderThickness());
        DrawControlHighlight(drawList, pos, size, radius);
    }

    public static void DrawCardBackground(Vector2 pos, Vector2 size, bool active, float hoverProgress)
    {
        var tokens = OmniTheme.Tokens;
        var drawList = ImGui.GetWindowDrawList();
        var radius = OmniTheme.Scale(tokens.ButtonRadius);
        hoverProgress = Math.Clamp(hoverProgress, 0f, 1f);
        if (OmniTheme.UsesMaterial)
        {
            DrawGlassSurface(drawList, pos, size,
                Vector4.Lerp(active ? tokens.Accent : tokens.Surface, OmniTheme.HoverBackground, hoverProgress),
                radius, sampleBackground: false);
            return;
        }
        var border = OmniTheme.BorderThickness() + OmniTheme.Scale(1f) * hoverProgress;
        var glowExtent = OmniTheme.Scale(6f) * hoverProgress;
        var shadowOffset = OmniTheme.Scale(MathF.Max(0f, tokens.ShadowOffset));
        drawList.PushClipRect(
            pos - new Vector2(border + glowExtent),
            pos + size + new Vector2(border + MathF.Max(glowExtent, shadowOffset)),
            true);
        DrawControlShadow(drawList, pos, size, radius);
        if (hoverProgress > 0.01f)
        {
            ImGuiOm.AddGlowRect(
                drawList,
                pos,
                pos + size,
                OmniTheme.Color(tokens.Border with { W = 0.18f * hoverProgress }),
                radius,
                OmniTheme.Scale(6f) * hoverProgress,
                10);
        }

        drawList.AddRectFilled(pos, pos + size, OmniTheme.Color(active ? tokens.Accent : tokens.Surface), radius);
        DrawControlFrame(
            drawList,
            pos,
            size,
            radius,
            border);
        DrawControlHighlight(drawList, pos, size, radius);
        drawList.PopClipRect();
    }

    public static void DrawLogoPlaceholder(Vector2 pos, Vector2 size)
    {
        var tokens = OmniTheme.Tokens;
        var drawList = ImGui.GetWindowDrawList();
        var radius = OmniTheme.Scale(tokens.ButtonRadius);
        drawList.AddRectFilled(pos, pos + size, OmniTheme.Color(tokens.Accent), radius);
        DrawControlFrame(drawList, pos, size, radius, OmniTheme.BorderThickness());
        DrawControlHighlight(drawList, pos, size, radius);
        DrawTextCentered("O", pos, size, tokens.Text);
    }

    internal static void DrawControlShadow(Vector2 pos, Vector2 size, float radius) =>
        DrawControlShadow(ImGui.GetWindowDrawList(), pos, size, radius);

    public static void DrawGlassSurface(
        ImDrawListPtr drawList, Vector2 pos, Vector2 size, Vector4 fill, float radius,
        float opacity = 1f, bool sampleBackground = true, ImDrawFlags corners = ImDrawFlags.RoundCornersAll)
    {
        MaterialPainter.Draw(drawList, pos, size, fill, radius, opacity, sampleBackground, corners);
    }

    private static void DrawMaterialControl(
        ImDrawListPtr drawList, Vector2 pos, Vector2 size, Vector4 fill, float radius, uint id) =>
        MaterialPainter.Draw(drawList, pos, size, fill, radius, 1f, false, ImDrawFlags.RoundCornersAll, id);

    internal static void DrawControlFrame(Vector2 pos, Vector2 size, float radius) =>
        DrawControlFrame(ImGui.GetWindowDrawList(), pos, size, radius, OmniTheme.BorderThickness());

    public static void HelpTooltip(string tooltip, bool requireItemHover = true)
    {
        if ((requireItemHover && !ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) || string.IsNullOrWhiteSpace(tooltip))
        {
            return;
        }

        using var tooltipFont = BeginHelpTooltip();
        ImGui.PushTextWrapPos(
            ImGui.GetCursorPosX() + MathF.Min(
                OmniTheme.Scale(520f),
                MathF.Max(1f, ImGui.GetMainViewport().WorkSize.X - OmniTheme.Scale(32f))));
        ImGui.TextUnformatted(tooltip);
        ImGui.PopTextWrapPos();
        EndHelpTooltip();
    }

    public static void HelpTooltip(ReadOnlySeString tooltip, Vector4? textColor = null)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) || tooltip.IsEmpty)
        {
            return;
        }

        using var tooltipFont = BeginHelpTooltip();
        ImGuiHelpers.SeStringWrapped(
            tooltip,
            new SeStringDrawParams
            {
                Color = textColor is { } color ? OmniTheme.Color(color) : null,
                WrapWidth = MathF.Min(
                    OmniTheme.Scale(520f),
                    MathF.Max(1f, ImGui.GetMainViewport().WorkSize.X - OmniTheme.Scale(32f)))
            });
        EndHelpTooltip();
    }

    private static IDisposable BeginHelpTooltip()
    {
        var font = OmniFonts.GetUIFont().Push();
        var tokens = OmniTheme.Tokens;
        ImGui.PushStyleColor(ImGuiCol.PopupBg, OmniTheme.TooltipBackground with { W = 1f });
        ImGui.PushStyleColor(ImGuiCol.Border, tokens.Border);
        ImGui.PushStyleColor(ImGuiCol.Text, tokens.Text);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, OmniTheme.Scale(new Vector2(10f, 7f)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, OmniTheme.Scale(tokens.BorderRadius));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, OmniTheme.Scale(tokens.BorderRadius));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, OmniTheme.BorderThickness());
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, OmniTheme.BorderThickness());
        ImGui.BeginTooltip();
        if (OmniTheme.UsesMaterial)
        {
            DrawGlassSurface(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), ImGui.GetWindowSize(),
                OmniTheme.TooltipBackground with { W = 0.92f }, OmniTheme.Scale(tokens.BorderRadius));
        }
        ImGui.SetWindowFontScale(1f);
        return font;
    }

    private static void EndHelpTooltip()
    {
        ImGui.EndTooltip();
        ImGui.PopStyleVar(6);
        ImGui.PopStyleColor(3);
    }

    private static void DrawHelpIcon(float lineTop, float lineHeight, float verticalOffset = 0f)
        => DrawInlineIcon(
            FontAwesomeIcon.InfoCircle,
            lineTop,
            lineHeight,
            OmniTheme.Tokens.Text with { W = 0.70f },
            verticalOffset);

    private static void DrawInlineIcon(
        FontAwesomeIcon icon,
        float lineTop,
        float lineHeight,
        Vector4 color,
        float verticalOffset = 0f)
    {
        var iconText = icon.ToIconString();
        var iconSize = GetScaledIconSize(iconText);
        var position = new Vector2(ImGui.GetCursorScreenPos().X, lineTop);
        ImGui.SetCursorScreenPos(position);
        ImGui.Dummy(new Vector2(iconSize.X, lineHeight));
        DrawScaledIcon(
            ImGui.GetWindowDrawList(),
            iconText,
            position + new Vector2(iconSize.X * 0.5f, lineHeight * 0.5f + verticalOffset),
            color);
    }

    private static Vector2 GetScaledIconSize(string icon, float glyphScale = ICON_GLYPH_SCALE) =>
        ImGui.CalcTextSize(icon) * glyphScale;

    private static unsafe void DrawTitleGlyph(FontAwesomeIcon icon, Vector2 center, float targetSize)
    {
        var iconText = icon.ToIconString();
        var font = ImGui.GetFont();
        var glyph = new ImFontGlyphPtr(font.FindGlyph(iconText[0]));
        var scale = targetSize / MathF.Max(glyph.X1 - glyph.X0, glyph.Y1 - glyph.Y0);
        var position = center - new Vector2(glyph.X0 + glyph.X1, glyph.Y0 + glyph.Y1) * (scale * 0.5f);
        ImGui.GetWindowDrawList().AddText(
            font,
            font.FontSize * scale,
            new Vector2(MathF.Round(position.X), MathF.Round(position.Y)),
            OmniTheme.Color(OmniTheme.Tokens.Text),
            iconText);
    }

    private static void DrawScaledIcon(
        ImDrawListPtr drawList,
        string icon,
        Vector2 center,
        Vector4 color,
        float horizontalOffset = 0f,
        float glyphScale = ICON_GLYPH_SCALE)
    {
        var iconSize = GetScaledIconSize(icon, glyphScale);
        drawList.AddText(
            ImGui.GetFont(),
            ImGui.GetFontSize() * glyphScale,
            center - iconSize * 0.5f + OmniTheme.Scale(new Vector2(horizontalOffset, 1f)),
            OmniTheme.Color(color),
            icon);
    }

    private static void DrawAvailabilityHelpTooltip(string? introduction = null)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            return;
        }

        using var tooltipFont = BeginHelpTooltip();
        if (introduction is not null)
        {
            foreach (var line in introduction.Split('\n'))
            {
                HelpTooltipLine(
                    line,
                    $"[{OmniLoc.Get("TreeHouse.Status.VerificationRequired")}]",
                    OmniTheme.AvailabilityColor(ItemAvailability.Unobtainable));
            }
        }

        HelpTooltipLine(
            OmniLoc.Get("ItemSearch.Availability.Help.Purchasable"),
            OmniLoc.Get(ItemAvailability.Purchasable),
            OmniTheme.AvailabilityColor(ItemAvailability.Purchasable));
        HelpTooltipLine(
            OmniLoc.Get("ItemSearch.Availability.Help.Obtainable"),
            OmniLoc.Get(ItemAvailability.Obtainable),
            OmniTheme.AvailabilityColor(ItemAvailability.Obtainable));
        HelpTooltipLine(
            OmniLoc.Get("ItemSearch.Availability.Help.Unobtainable"),
            OmniLoc.Get(ItemAvailability.Unobtainable),
            OmniTheme.AvailabilityColor(ItemAvailability.Unobtainable));
        EndHelpTooltip();
    }

    public static void HelpTooltipLine(string template, string markerText, Vector4 markerColor)
    {
        const string MARKER = "{0}";
        var markerIndex = template.IndexOf(MARKER, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            ImGui.TextUnformatted(template);
            return;
        }

        ImGui.TextUnformatted(template[..markerIndex]);
        ImGui.SameLine(0f, 0f);
        ImGui.TextColored(markerColor, markerText);
        ImGui.SameLine(0f, 0f);
        ImGui.TextUnformatted(template[(markerIndex + MARKER.Length)..]);
    }

    private static string DrawLeadingControlLabel(string id, ref float width)
    {
        var separator = id.IndexOf("##", StringComparison.Ordinal);
        var label = separator < 0 ? id : id[..separator];
        if (label.Length == 0)
        {
            return id;
        }

        var spacing = ImGui.GetStyle().ItemInnerSpacing.X;
        var labelWidth = ImGui.CalcTextSize(label).X;
        var available = ImGui.GetContentRegionAvail().X;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (available >= labelWidth + spacing + MathF.Min(width, OmniTheme.Scale(64f)))
        {
            ImGui.SameLine(0f, spacing);
        }
        width = MathF.Max(1f, MathF.Min(width, ImGui.GetContentRegionAvail().X));
        // 保留完整标识以区分同名控件，最后提交的项目仍是输入框，供编辑完成检测使用。
        return $"###{id}";
    }

    private static NativeControlFrame BeginNativeControl(
        string id,
        float width,
        bool fillSurface) =>
        BeginNativeControl(
            id,
            new Vector2(MathF.Max(1f, width), MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight())),
            fillSurface);

    private static NativeControlFrame BeginNativeControl(
        string id,
        Vector2 requestedSize,
        bool fillSurface)
    {
        var tokens = OmniTheme.Tokens;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Max(1f, requestedSize.X), MathF.Max(1f, requestedSize.Y));
        var radius = OmniTheme.CheckboxRounding();
        var drawList = ImGui.GetWindowDrawList();
        var itemID = ImGui.GetID(id);
        var hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + size, true) && !GlassMotion.IsDisabled;
        if (OmniTheme.UsesMaterial)
        {
            radius = OmniTheme.Scale(tokens.ButtonRadius);
            DrawMaterialControl(drawList, pos, size,
                GlassMotion.ControlFill(itemID, tokens.Surface, hovered, GlassMotion.ActiveItemID == itemID), radius, itemID);
            ImGui.SetNextItemWidth(size.X);
            return new NativeControlFrame(drawList, pos, size, radius, itemID);
        }
        DrawControlShadow(drawList, pos, size, radius);
        if (fillSurface)
        {
            drawList.AddRectFilled(
                pos,
                pos + size,
                OmniTheme.Color(tokens.Surface with { W = 1f }),
                radius,
                ImDrawFlags.RoundCornersAll);
        }

        ImGui.SetNextItemWidth(size.X);
        return new NativeControlFrame(drawList, pos, size, radius, itemID);
    }

    private static void EndNativeControl(NativeControlFrame frame, bool? active = null)
    {
        if (OmniTheme.UsesMaterial)
        {
            var focused = active ?? (ImGui.IsItemActive() || ImGui.IsItemFocused());
            var focus = GlassMotion.Value(frame.ID, 2, focused ? 0.65f : 0f, 0.14f);
            if (focus > 0.01f)
            {
                frame.DrawList.AddRect(frame.Pos, frame.Pos + frame.Size,
                    OmniTheme.Color(OmniTheme.ControlAccent with { W = focus }),
                    frame.Radius, ImDrawFlags.RoundCornersAll, OmniTheme.BorderThickness());
            }
            return;
        }
        DrawControlFrame(frame.DrawList, frame.Pos, frame.Size, frame.Radius, OmniTheme.BorderThickness());
        DrawControlHighlight(frame.DrawList, frame.Pos, frame.Size, frame.Radius);
    }

    private readonly record struct NativeControlFrame(
        ImDrawListPtr DrawList, Vector2 Pos, Vector2 Size, float Radius, uint ID);

    private static void DrawControlShadow(ImDrawListPtr drawList, Vector2 pos, Vector2 size, float radius, float opacity = 1f)
    {
        var tokens = OmniTheme.Tokens;
        if (tokens.Shadow.W <= 0f || size.X <= 1f || size.Y <= 1f)
        {
            return;
        }

        var offset = OmniTheme.Scale(MathF.Max(0f, tokens.ShadowOffset));
        if (OmniTheme.IsGlass)
        {
            for (var band = 4; band >= 1; band--)
            {
                var spread = OmniTheme.Scale(band * 1.25f);
                drawList.AddRect(pos + new Vector2(0f, offset) - new Vector2(spread),
                    pos + size + new Vector2(0f, offset) + new Vector2(spread),
                    OmniTheme.Color(tokens.Shadow with { W = tokens.Shadow.W * opacity * (5 - band) / 10f }),
                    radius + spread, ImDrawFlags.RoundCornersAll, OmniTheme.Scale(1.5f));
            }
            return;
        }
        drawList.AddRectFilled(
            pos + new Vector2(offset),
            pos + size + new Vector2(offset),
            OmniTheme.Color(tokens.Shadow),
            radius,
            ImDrawFlags.RoundCornersAll);
    }

    internal static void DrawControlHighlight(ImDrawListPtr drawList, Vector2 pos, Vector2 size, float radius)
    {
        var alpha = OmniTheme.Tokens.HighlightStrength;
        if (alpha <= 0f || size.X <= 1f || size.Y <= 1f)
        {
            return;
        }

        radius = MathF.Min(MathF.Max(0f, radius), MathF.Min(size.X, size.Y) * 0.5f);
        var height = Math.Clamp(
            size.Y * 0.22f + radius * 0.25f,
            size.Y * 0.12f,
            size.Y * 0.38f);
        var bandHeight = height / BORDER_HIGHLIGHT_BANDS;
        var max = pos + size;
        for (var i = 0; i < BORDER_HIGHLIGHT_BANDS; i++)
        {
            var y0 = pos.Y + i * bandHeight;
            var y1 = i == BORDER_HIGHLIGHT_BANDS - 1 ? pos.Y + height : y0 + bandHeight;
            drawList.PushClipRect(new Vector2(pos.X, y0), new Vector2(max.X, y1), true);
            drawList.AddRectFilled(
                pos,
                max,
                OmniTheme.Color(Vector4.One with { W = alpha * (1f - (i + 0.5f) / BORDER_HIGHLIGHT_BANDS) }),
                radius,
                ImDrawFlags.RoundCornersAll);
            drawList.PopClipRect();
        }
    }

    internal static void DrawControlFrame(ImDrawListPtr drawList, Vector2 pos, Vector2 size, float radius, float thickness)
    {
        if (thickness <= 0.01f || size.X <= thickness || size.Y <= thickness)
        {
            return;
        }

        var half = thickness * 0.5f;
        drawList.AddRect(
            pos + new Vector2(half),
            pos + size - new Vector2(half),
            OmniTheme.Color(OmniTheme.Tokens.Border),
            MathF.Max(0f, radius - half),
            ImDrawFlags.RoundCornersAll,
            thickness);
    }

    private static string DisplayLabel(string label) =>
        label.Contains("##", StringComparison.Ordinal) ? label[..label.IndexOf("##", StringComparison.Ordinal)] : label;

    private readonly record struct FavoriteParticle(
        Vector2 Origin,
        Vector2 Velocity,
        float Scale,
        float Lifetime,
        double StartedAt);
}
