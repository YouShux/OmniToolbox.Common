using System.Drawing;
using Dalamud.Interface;
using OmenTools.Extensions;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using TreeToolKit;

namespace OmniToolbox.UI;

public sealed class OmniRecordWindow
(
    IOmniRecordWindowSettings settings,
    string                    windowID,
    string                    collapsedID,
    Vector2                   minimumSize,
    Action                    saveConfig
)
{
    private bool restoreLayout = true;
    private bool focusRequested;
    private bool geometryDirty;
    private bool collapsedIconDragged;
    private float lastScale;
    private Vector2 lastViewportSize;
    private DateTime? expansionStartUTC;
    private Vector2 expansionFromPosition;
    private Vector2 expansionFromSize;

    public bool IsFocused { get; private set; }

    public void Open(bool requestFocus = true)
    {
        settings.Collapsed = false;
        expansionStartUTC = null;
        focusRequested = requestFocus;
        RestoreLayout();
    }

    public void Close()
    {
        if (geometryDirty || collapsedIconDragged)
            saveConfig();
        Reset();
    }

    public void RestoreLayout()
    {
        restoreLayout = true;
        lastScale = 0f;
    }

    public void Reset()
    {
        IsFocused = false;
        geometryDirty = false;
        collapsedIconDragged = false;
        expansionStartUTC = null;
        focusRequested = false;
        RestoreLayout();
    }

    public void Collapse(Vector2 buttonPosition)
    {
        settings.CollapsedPosition = buttonPosition;
        settings.Collapsed = true;
        expansionStartUTC = null;
        saveConfig();
    }

    public void Draw(string title, string expandTooltip, Action drawContent, Action? drawCollapsedContent = null)
    {
        using var placement = new OmniWindowPlacement.WindowScope(settings.Collapsed ? collapsedID : windowID);
        using var font = Fonts.GetUIFont().Push();
        using var theme = new ComicStyleScope();
        using var layoutScale = new OmniTheme.ScaleScope(OmniTheme.ConfiguredScaleValue);
        IsFocused = false;
        var fontScale = MathF.Max(0.01f, settings.GlobalScale);
        var scale = OmniTheme.ScaleValue * fontScale;
        if (settings.Collapsed)
        {
            DrawCollapsed(expandTooltip, fontScale, scale, drawCollapsedContent);
            return;
        }

        var viewportSize = OmniWindowPlacement.GetWorkArea(windowID).Size;
        var scaleChanged = MathF.Abs(lastScale - scale) > 0.001f || lastViewportSize != viewportSize;
        lastViewportSize = viewportSize;
        var targetPosition = settings.Position;
        var targetSize = settings.Size * scale;
        var positionCondition = restoreLayout ? ImGuiCond.Always : ImGuiCond.FirstUseEver;
        var sizeCondition = restoreLayout || scaleChanged ? ImGuiCond.Always : ImGuiCond.FirstUseEver;
        var animatingExpansion = false;
        if (expansionStartUTC is { } start)
        {
            var progress = Math.Clamp((float)(DateTime.UtcNow - start).TotalSeconds / EXPANSION_SECONDS, 0f, 1f);
            if (progress < 1f)
            {
                var eased = progress * progress * (3f - 2f * progress);
                targetPosition = Vector2.Lerp(expansionFromPosition, targetPosition, eased);
                targetSize = Vector2.Lerp(expansionFromSize, targetSize, eased);
                animatingExpansion = true;
            }
            else
                expansionStartUTC = null;
            positionCondition = ImGuiCond.Always;
            sizeCondition = ImGuiCond.Always;
        }

        targetSize = OmniTheme.ClampWindowSize(targetSize);
        targetSize = new Vector2(MathF.Round(targetSize.X), MathF.Round(targetSize.Y));
        targetPosition = OmniTheme.ClampWindowPosition(targetPosition, targetSize);
        ImGui.SetNextWindowPos(targetPosition, positionCondition);
        ImGui.SetNextWindowSize(targetSize, sizeCondition);
        ImGui.SetNextWindowSizeConstraints(
            Vector2.Min(minimumSize * scale, animatingExpansion ? targetSize : viewportSize), viewportSize);
        OmniWindowPlacement.SetNextWindowPosition(windowID,
            positionCondition == ImGuiCond.Always ? targetPosition : null);
        if (focusRequested)
            ImGui.SetNextWindowFocus();
        focusRequested = false;
        restoreLayout = false;
        lastScale = scale;

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar;
        if (settings.Locked || animatingExpansion)
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(6f) * scale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(6f, 3f) * scale)
            .Push(ImGuiStyleVar.CellPadding, new Vector2(6f, 3f) * scale)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(5f, 4f) * scale)
            .Push(ImGuiStyleVar.WindowRounding, 8f * scale)
            .Push(ImGuiStyleVar.WindowBorderSize, OmniTheme.BorderThickness() * fontScale);
        using var colors = ImRaii.PushColor(ImGuiCol.WindowBg, new Vector4(0.045f, 0.047f, 0.052f, settings.Opacity))
            .Push(ImGuiCol.Border, KnownColor.LightSlateGray.ToVector4() with { W = 0.42f })
            .Push(ImGuiCol.TableHeaderBg, new Vector4(1f, 1f, 1f, 0.035f));
        try
        {
            if (ImGui.Begin($"{title}{windowID}", flags))
            {
                ImGui.SetWindowFontScale(fontScale);
                IsFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
                drawContent();
                if (!animatingExpansion && !settings.Collapsed && !restoreLayout)
                    UpdateGeometry(scale);
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    public static bool DrawHeaderButton(string id, FontAwesomeIcon icon, Vector2 size, string tooltip)
    {
        var position = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var fontSize = ImGui.GetFontSize() * 0.82f;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var text = icon.ToIconString();
            var textSize = ImGui.CalcTextSize(text) * (fontSize / ImGui.GetFontSize());
            ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), fontSize,
                position + Vector2.Max(Vector2.Zero, (size - textSize) * 0.5f),
                OmniTheme.Color(OmniTheme.Tokens.Text with
                {
                    W = OmniTheme.Tokens.Text.W * (ImGui.IsItemHovered() || ImGui.IsItemActive() ? 1f : 0.9f)
                }), text);
        }
        if (ImGui.IsItemHovered())
            OmniControls.HelpTooltip(tooltip);
        return clicked;
    }

    public static void SetWindowFontSize(float fontSize)
    {
        ImGui.SetWindowFontScale(1f);
        ImGui.SetWindowFontScale(fontSize / ImGui.GetFontSize());
    }

    private void DrawCollapsed(string tooltip, float fontScale, float scale, Action? drawContent)
    {
        var size = new Vector2(COLLAPSED_SIZE) * scale;
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove;
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        OmniWindowPlacement.SetNextWindowPosition(collapsedID, settings.CollapsedPosition);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(4f) * scale)
            .Push(ImGuiStyleVar.WindowBorderSize, scale)
            .Push(ImGuiStyleVar.WindowMinSize, size);
        using var colors = ImRaii.PushColor(ImGuiCol.WindowBg, OmniTheme.Tokens.Background with { W = settings.Opacity });
        try
        {
            if (!ImGui.Begin(collapsedID, flags))
                return;
            ImGui.SetWindowFontScale(fontScale);
            IsFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            drawContent?.Invoke();
            var min = ImGui.GetCursorScreenPos();
            var iconSize = new Vector2(26f) * scale;
            ImGui.InvisibleButton("##Expand", iconSize);
            if (!settings.Locked && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                collapsedIconDragged = true;
                var delta = ImGui.GetIO().MouseDelta;
                settings.CollapsedPosition = OmniWindowPlacement.ClampPosition(ImGui.GetWindowPos() + delta, size, true);
                settings.Position += delta;
                ImGui.SetWindowPos(settings.CollapsedPosition);
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            }
            if (ImGui.IsItemDeactivated())
            {
                if (collapsedIconDragged)
                    saveConfig();
                else if (ImGui.IsItemHovered())
                {
                    expansionFromPosition = ImGui.GetWindowPos();
                    expansionFromSize = ImGui.GetWindowSize();
                    expansionStartUTC = DateTime.UtcNow;
                    settings.Collapsed = false;
                    restoreLayout = true;
                    focusRequested = true;
                    saveConfig();
                }
                collapsedIconDragged = false;
            }
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = FontAwesomeIcon.ChevronCircleDown.ToIconString();
                var textSize = ImGui.CalcTextSize(icon);
                ImGui.GetWindowDrawList().AddText(min + Vector2.Max(Vector2.Zero, (iconSize - textSize) * 0.5f),
                    ImGui.GetColorU32(ImGuiCol.Text), icon);
            }
            if (ImGui.IsItemHovered())
                OmniControls.HelpTooltip(tooltip);
        }
        finally
        {
            ImGui.End();
        }
    }

    private void UpdateGeometry(float scale)
    {
        if (settings.Locked)
            return;
        var position = ImGui.GetWindowPos();
        if (Vector2.DistanceSquared(position, settings.Position) > 0.25f)
        {
            settings.Position = position;
            geometryDirty = true;
        }
        var expectedSize = settings.Size * scale;
        var size = OmniTheme.PreserveWindowSize(ImGui.GetWindowSize(), expectedSize);
        if (MathF.Abs(size.X - expectedSize.X) > 0.5f || MathF.Abs(size.Y - expectedSize.Y) > 0.5f)
        {
            settings.Size = Vector2.Max(size / scale, minimumSize);
            geometryDirty = true;
        }
        if (geometryDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            geometryDirty = false;
            saveConfig();
        }
    }

    #region 常量

    private const float COLLAPSED_SIZE = 34f;
    private const float EXPANSION_SECONDS = 0.15f;

    #endregion
}
