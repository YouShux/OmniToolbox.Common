using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public static partial class OmniControls
{
    public static bool NameEditPopup
    (
        string              id,
        string              title,
        string              nameLabel,
        ref string          name,
        int                 maxLength,
        Func<string, bool>? canSave = null
    )
    {
        using var popupStyle = new OmniStyleScope();
        using var popupPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, OmniTheme.PopupPadding());
        var saveLabel = OmniLoc.Get("Common.Save");
        var cancelLabel = OmniLoc.Get("Common.Cancel");
        var actionSize = Vector2.Max(CompactButtonSize(saveLabel), CompactButtonSize(cancelLabel));
        var actionsWidth = actionSize.X * 2f + ImGui.GetStyle().ItemSpacing.X;
        var viewportSize = OmniWindowPlacement.GetWorkArea().Size;
        var popupWidth = MathF.Min(MathF.Floor(viewportSize.X),
            MathF.Ceiling(MathF.Max(ImGui.GetTextLineHeight() * 24f, actionsWidth) + ImGui.GetStyle().WindowPadding.X * 2f));
        ImGui.SetNextWindowSize(new Vector2(popupWidth, 0f), ImGuiCond.Always);
        SetNextAutoResizeWindowSizeConstraints(new Vector2(popupWidth, 0f),
            new Vector2(popupWidth, viewportSize.Y));
        using var popup = ImRaii.Popup(id, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
            return false;

        DrawEditorPopupBackground(title);
        var inputWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X -
            ImGui.CalcTextSize(nameLabel).X - ImGui.GetStyle().ItemInnerSpacing.X);
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();
        var confirm = InputText(nameLabel, ref name, maxLength, inputWidth, ImGuiInputTextFlags.EnterReturnsTrue);
        var trimmedName = name.Trim();
        var valid = trimmedName.Length > 0 && (canSave?.Invoke(trimmedName) ?? true);

        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - actionsWidth) * 0.5f));
        using (ImRaii.Disabled(!valid))
            confirm |= SmallButton(saveLabel, false, actionSize);
        ImGui.SameLine();
        if (SmallButton(cancelLabel, false, actionSize))
        {
            ImGui.CloseCurrentPopup();
            return false;
        }

        if (!confirm || !valid)
            return false;

        name = trimmedName;
        ImGui.CloseCurrentPopup();
        return true;
    }

    public static void DrawEditorPopupBackground(string title)
    {
        using var theme = new OmniTheme.ColorScope(OmniTheme.BaseTokens);
        var frameInset = new Vector2(OmniTheme.ChromeFrameInset());
        var framePosition = ImGui.GetWindowPos() + frameInset;
        var frameSize = ImGui.GetWindowSize() - frameInset * 2f;
        var titleHeight = OmniTheme.TitleBarHeight();
        DrawWindowBackground(framePosition, frameSize, false);
        DrawTextCentered(title, framePosition, new Vector2(frameSize.X, titleHeight), OmniTheme.Tokens.Text);
        ImGui.Dummy(new Vector2(ImGui.CalcTextSize(title).X, titleHeight));
        var inset = new Vector2(OmniTheme.WindowInset());
        ImGui.SetCursorPosY(frameInset.Y + titleHeight + inset.Y * 2f);
        var panelPosition = framePosition + new Vector2(0f, titleHeight + inset.Y);
        var panelSize = framePosition + frameSize - panelPosition;
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(framePosition, framePosition + frameSize, false);
        if (OmniTheme.UsesMaterial)
            DrawGlassSurface(drawList, panelPosition, panelSize, OmniTheme.Tokens.Surface,
                OmniTheme.Scale(OmniTheme.Tokens.BorderRadius), sampleBackground: false);
        else
            DrawPanelBackground(panelPosition, panelSize, OmniTheme.Tokens.Surface);
        drawList.PopClipRect();
    }
}
