using Dalamud.Game.ClientState.Objects.Enums;
using OmenTools;
using OmenTools.Dalamud.Services.Game.Object.Abstractions;
using OmenTools.OmenService;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.UI;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.Modules;

public static class NearbyPlayerGuard
{
    public static float ClampRange(float range) =>
        float.IsFinite(range) ? Math.Clamp(range, 30f, 100f) : 50f;

    internal static bool IsWithinRange
    (
        Vector3 origin,
        Vector3 candidate,
        float   range
    )
    {
        var radius = ClampRange(range);
        return Vector3.DistanceSquared(origin, candidate) <= radius * radius;
    }

    public static bool IsSolo
    {
        get
        {
            if (LocalPlayerState.IsInCrossRealmParty || LocalPlayerState.IsInParty)
                return false;

            return !HasOtherPlayers();
        }
    }

    public static bool HasOtherPlayers()
    {
        var objects = DalamudServices.ObjectTable;
        if (objects.LocalPlayer is not { } player)
            return true;

        foreach (var candidate in objects.SearchObjects(
                     static gameObject => gameObject.ObjectKind == ObjectKind.Pc,
                     IObjectTable.CharactersRange))
        {
            if (candidate.Address != player.Address)
                return true;
        }

        return false;
    }

    public static bool IsBlocked
    (
        bool  enabled,
        float range
    )
    {
        if (!enabled)
            return false;

        var objects = DalamudServices.ObjectTable;
        if (objects.LocalPlayer is not { } player)
            return true;

        foreach (var candidate in objects)
        {
            if (candidate is { ObjectKind: ObjectKind.Pc } && candidate.Address != player.Address &&
                IsWithinRange(player.Position, candidate.Position, range))
                return true;
        }

        return false;
    }

    public static bool DrawSettings
    (
        OmniModuleBase module,
        ref bool       enabled,
        ref float      range
    )
    {
        var playersLabel = OmniLoc.Get(module, "DisableWhenPlayersAround");
        var rangeSize = OmniControls.MeasureGroup(
            [ImGui.CalcTextSize(OmniLoc.Get(module, "PlayerDetectionRange")),
                OmniControls.MeasureFloatInput(range, "%.0f m", OmniTheme.Scale(180f))],
            ImGui.GetStyle().ItemInnerSpacing.X);
        using var table = OmniControls.SettingsTable(
            "##nearbyPlayerSettings",
            [OmniControls.MeasureCheckbox(playersLabel), rangeSize],
            ["##nearbyPlayers", "##playerDetectionRange"],
            flags: ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX, columnsPerRow: 4);
        if (!table)
            return false;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var changed = OmniControls.Checkbox(playersLabel, ref enabled);
        ImGui.TableNextColumn();
        return DrawRange(module, enabled, ref range) || changed;
    }

    public static bool DrawRange
    (
        OmniModuleBase module,
        bool           enabled,
        ref float      range,
        float          width = 0f
    )
    {
        using var disabled = ImRaii.Disabled(!enabled);
        OmniControls.SliderFloat(
            OmniLoc.Get(module, "PlayerDetectionRange"),
            ref range,
            30f,
            100f,
            "%.0f m",
            width > 0f ? width : OmniTheme.Scale(180f));
        return ImGui.IsItemDeactivatedAfterEdit();
    }
}
