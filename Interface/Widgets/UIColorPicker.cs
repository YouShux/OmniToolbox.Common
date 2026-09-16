using System.Drawing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.OmenService;

namespace OmniToolbox.UI.Controls;

internal static unsafe class UIColorPicker
{
    public static Vector3 Resolve(
        int colorID,
        bool useCustomColor,
        Vector3 customColor,
        Vector4? fallbackColor = null)
        => useCustomColor ? Normalize(customColor) : GetColor(colorID, fallbackColor);

    public static Vector3 GetColor(int colorID, Vector4? fallbackColor = null)
    {
        var color = TryGetColor(colorID, out var resolved)
            ? resolved
            : fallbackColor ?? KnownColor.White.ToVector4();
        return Normalize(new(color.X, color.Y, color.Z));
    }

    public static bool Draw(
        string id,
        ref Vector3 color,
        ImGuiColorEditFlags displayFlags = ImGuiColorEditFlags.None)
    {
        color = Normalize(color);
        if (!OmniControls.ColorEdit($"##{id}Color", ref color, displayFlags))
        {
            OmniControls.HelpTooltip(OmniLoc.Get("Common.UiColorCustom"));
            return false;
        }

        color = Normalize(color);
        return true;
    }

    public static Vector4 ToRgba(Vector3 color) => new(Normalize(color), 1f);

    public static byte[] BuildColorPayload(Vector3 color, bool pop)
    {
        var builder = new Lumina.Text.SeStringBuilder();
        if (pop)
        {
            builder.PopColor();
        }
        else
        {
            builder.PushColorRgba(ToRgba(color));
        }

        return builder.GetViewAsMemory().ToArray();
    }

    private static Vector3 Normalize(Vector3 color) => new(
        NormalizeComponent(color.X),
        NormalizeComponent(color.Y),
        NormalizeComponent(color.Z));

    private static float NormalizeComponent(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;

    private static bool TryGetColor(int id, out Vector4 color)
    {
        var stage = AtkStage.Instance();
        if (id <= 0 || stage == null || stage->AtkUIColorHolder == null)
        {
            color = default;
            return false;
        }

        color = ImGui.ColorConvertU32ToFloat4(stage->AtkUIColorHolder->GetColor(true, (uint)id));
        return true;
    }
}
