namespace OmniToolbox.UI.Theme;

public sealed class ComicStyleScope : IDisposable
{
    private const int VAR_COUNT = 9;
    private readonly int colorCount;
    private bool disposed;

    public ComicStyleScope()
    {
        var tokens = OmniTheme.Tokens;
        var controlAccent = OmniTheme.ControlAccent;
        var pushedColors = 0;
        PushColor(ImGuiCol.Text, tokens.Text);
        PushColor(ImGuiCol.TextDisabled, tokens.Text with { W = 0.70f });
        PushColor(ImGuiCol.WindowBg, tokens.Background);
        PushColor(
            ImGuiCol.ChildBg,
            tokens.Surface with { W = OmniTheme.UsesDarkPalette ? 0.28f : 0.18f });
        PushColor(ImGuiCol.PopupBg, OmniTheme.UsesMaterial
            ? OmniTheme.TooltipBackground with { W = 0.98f }
            : tokens.Primary with { W = 1f });
        PushColor(ImGuiCol.Border, tokens.Border);
        PushColor(ImGuiCol.BorderShadow, OmniTheme.UsesMaterial ? Vector4.Zero : tokens.Shadow);
        PushColor(ImGuiCol.Button, tokens.Surface);
        PushColor(ImGuiCol.ButtonHovered, OmniTheme.UsesMaterial || OmniTheme.UsesDarkPalette ? OmniTheme.HoverBackground : Lighten(tokens.Surface, 0.08f));
        PushColor(ImGuiCol.ButtonActive, OmniTheme.UsesMaterial || OmniTheme.UsesDarkPalette ? OmniTheme.ActiveBackground : Darken(tokens.Secondary, 0.06f));
        PushColor(ImGuiCol.FrameBg, tokens.Surface);
        PushColor(ImGuiCol.FrameBgHovered, OmniTheme.UsesMaterial || OmniTheme.UsesDarkPalette ? OmniTheme.HoverBackground : Lighten(tokens.Surface, 0.08f));
        PushColor(ImGuiCol.FrameBgActive, OmniTheme.UsesMaterial || OmniTheme.UsesDarkPalette ? OmniTheme.ActiveBackground : Darken(tokens.Secondary, 0.06f));
        PushColor(ImGuiCol.CheckMark, tokens.Text);
        PushColor(ImGuiCol.Header, OmniTheme.UsesMaterial ? tokens.Accent :
            OmniTheme.UsesDarkPalette ? OmniTheme.ActiveBackground : tokens.Primary);
        PushColor(ImGuiCol.HeaderHovered, OmniTheme.UsesMaterial || OmniTheme.UsesDarkPalette ? OmniTheme.HoverBackground : tokens.Primary with { W = 0.88f });
        PushColor(ImGuiCol.HeaderActive, tokens.Secondary);
        PushColor(
            ImGuiCol.TableHeaderBg,
            OmniTheme.UsesDarkPalette ? tokens.Primary with { W = 0.70f } : tokens.Surface);
        PushColor(ImGuiCol.TableBorderStrong, tokens.Border);
        PushColor(ImGuiCol.TableBorderLight, tokens.Border with { W = OmniTheme.UsesDarkPalette ? 0.34f : 0.30f });
        PushColor(
            ImGuiCol.TableRowBg,
            Vector4.Zero);
        PushColor(
            ImGuiCol.TableRowBgAlt,
            OmniTheme.UsesMaterial ? tokens.Surface with { W = 0.04f } :
            OmniTheme.UsesDarkPalette ? tokens.Secondary with { W = 0.10f } : tokens.Surface with { W = 0.10f });
        PushColor(ImGuiCol.ScrollbarBg, tokens.Primary with { W = 0.16f });
        PushColor(ImGuiCol.ScrollbarGrab, controlAccent with { W = 0.72f });
        PushColor(ImGuiCol.ScrollbarGrabHovered, controlAccent with { W = 0.84f });
        PushColor(ImGuiCol.ScrollbarGrabActive, controlAccent);
        PushColor(ImGuiCol.SliderGrab, controlAccent);
        PushColor(ImGuiCol.SliderGrabActive, controlAccent);
        PushColor(ImGuiCol.Separator, tokens.Primary with { W = 0.48f });
        PushColor(ImGuiCol.SeparatorHovered, tokens.Primary with { W = 0.70f });
        PushColor(ImGuiCol.SeparatorActive, tokens.Primary);
        PushColor(ImGuiCol.ResizeGrip, tokens.Secondary with { W = 0.30f });
        PushColor(ImGuiCol.ResizeGripHovered, tokens.Secondary with { W = 0.50f });
        PushColor(ImGuiCol.ResizeGripActive, tokens.Secondary with { W = 0.70f });
        PushColor(ImGuiCol.Tab, tokens.Surface);
        PushColor(ImGuiCol.TabHovered, tokens.Secondary);
        PushColor(ImGuiCol.TabActive, tokens.Secondary);
        PushColor(ImGuiCol.TabUnfocused, tokens.Surface with { W = 0.70f });
        PushColor(ImGuiCol.TabUnfocusedActive, tokens.Secondary with { W = 0.70f });
        PushColor(ImGuiCol.TextSelectedBg, tokens.Accent with { W = 0.30f });
        PushColor(ImGuiCol.DragDropTarget, tokens.Accent);
        PushColor(ImGuiCol.NavHighlight, OmniTheme.UsesMaterial ? controlAccent : tokens.Accent);
        PushColor(ImGuiCol.TitleBg, tokens.Primary);
        PushColor(ImGuiCol.TitleBgActive, tokens.Primary);
        PushColor(ImGuiCol.TitleBgCollapsed, tokens.Primary with { W = 0.54f });
        PushColor(ImGuiCol.MenuBarBg, tokens.Surface);
        colorCount = pushedColors;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, OmniTheme.Scale(tokens.WindowPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, OmniTheme.Scale(tokens.FramePadding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, OmniTheme.Scale(tokens.ItemSpacing));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, OmniTheme.Scale(tokens.BorderRadius));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, OmniTheme.Scale(tokens.BorderRadius));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, OmniTheme.Scale(tokens.ButtonRadius));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, OmniTheme.Scale(tokens.BorderThickness));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, OmniTheme.Scale(tokens.BorderThickness));

        void PushColor(ImGuiCol target, Vector4 color)
        {
            ImGui.PushStyleColor(target, color);
            pushedColors++;
        }
    }

    private static Vector4 Lighten(Vector4 color, float amount) => new(
        Math.Clamp(color.X + amount, 0f, 1f),
        Math.Clamp(color.Y + amount, 0f, 1f),
        Math.Clamp(color.Z + amount, 0f, 1f),
        color.W);

    private static Vector4 Darken(Vector4 color, float amount) => new(
        Math.Clamp(color.X - amount, 0f, 1f),
        Math.Clamp(color.Y - amount, 0f, 1f),
        Math.Clamp(color.Z - amount, 0f, 1f),
        color.W);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ImGui.PopStyleVar(VAR_COUNT);
        ImGui.PopStyleColor(colorCount);
    }
}
