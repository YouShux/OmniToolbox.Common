using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public sealed class TerritorySelector
{
    private static readonly List<TerritoryOption> TerritoryOptions = [];
    private static readonly HashSet<uint> TerritoryOptionIDs = [];
    private static bool TerritoryOptionsLoaded;

    private readonly string id;
    private readonly List<TerritoryOption> visibleTerritoryOptions = [];
    private string search = string.Empty;

    public TerritorySelector(string id)
    {
        this.id = id;
    }

    public bool Draw(HashSet<uint> selectedTerritoryIds, string emptyText)
    {
        EnsureTerritoryOptions();
        var changed = TerritoryOptionIDs.Count > 0 &&
                      selectedTerritoryIds.RemoveWhere(territoryID => !TerritoryOptionIDs.Contains(territoryID)) > 0;
        var header = string.Format(
            OmniLoc.Get("Common.TerritorySelector.Title"),
            selectedTerritoryIds.Count);
        if (!OmniControls.CollapsingHeader(
                $"{header}##{id}Header",
                ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            if (selectedTerritoryIds.Count == 0 && !string.IsNullOrEmpty(emptyText))
            {
                ImGui.TextDisabled(emptyText);
            }

            return changed;
        }

        var clientState = DService.Instance().ClientState;
        var currentTerritoryID = clientState.IsLoggedIn ? (uint)clientState.TerritoryType : 0;
        using (ImRaii.Disabled(currentTerritoryID == 0 || selectedTerritoryIds.Contains(currentTerritoryID)))
        {
            if (OmniControls.SmallButton(
                    $"{OmniLoc.Get("Common.TerritorySelector.AddCurrent")}##{id}AddCurrent",
                    false))
            {
                changed |= selectedTerritoryIds.Add(currentTerritoryID);
            }
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(currentTerritoryID == 0
            ? OmniLoc.Get("Common.TerritorySelector.LoggedOut")
            : string.Format(
                OmniLoc.Get("Common.TerritorySelector.Current"),
                GetTerritoryName(currentTerritoryID),
                currentTerritoryID));

        OmniControls.InputTextWithHint(
            $"##{id}Search",
            OmniLoc.Get("Common.TerritorySelector.SearchHint"),
            ref search,
            128,
            ImGui.GetContentRegionAvail().X);
        changed |= DrawTable(selectedTerritoryIds);

        if (selectedTerritoryIds.Count == 0 && !string.IsNullOrEmpty(emptyText))
        {
            ImGui.TextDisabled(emptyText);
        }

        return changed;
    }

    private bool DrawTable(HashSet<uint> selectedTerritoryIds)
    {
        var rows = GetVisibleTerritories(selectedTerritoryIds, search.Trim());
        if (rows.Count == 0)
        {
            ImGui.TextDisabled(OmniLoc.Get("Common.TerritorySelector.NoMatches"));
            return false;
        }

        var tableHeight =
            (OmniTheme.SmallButtonSize().Y + ImGui.GetStyle().CellPadding.Y * 2f) * 6f +
            OmniTheme.BorderThickness() * 2f;
        using var table = ImRaii.Table(
            $"##{id}Table",
            4,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingStretchProp,
            new Vector2(0f, tableHeight));
        if (!table)
        {
            return false;
        }

        ImGui.TableSetupColumn($"##{id}Check", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn(
            OmniLoc.Get("Common.TerritorySelector.Column.Map"),
            ImGuiTableColumnFlags.WidthStretch,
            1.35f);
        ImGui.TableSetupColumn(
            OmniLoc.Get("Common.TerritorySelector.Column.Region"),
            ImGuiTableColumnFlags.WidthStretch,
            1f);
        ImGui.TableSetupColumn(
            OmniLoc.Get("Common.TerritorySelector.Column.Id"),
            ImGuiTableColumnFlags.WidthFixed,
            OmniTheme.Scale(74f));
        ImGui.TableSetupScrollFreeze(0, 1);
        OmniControls.BeginTableHeaderRow();
        ImGui.TableNextColumn();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));
        var selectAll = TerritoryOptions.Count > 0 && selectedTerritoryIds.Count == TerritoryOptions.Count;
        OmniControls.CenterTableItem(new Vector2(OmniTheme.CheckboxSize()), OmniTheme.SmallButtonSize().Y);
        var changed = false;
        if (OmniControls.Checkbox($"##{id}SelectAll", ref selectAll))
        {
            if (selectAll)
            {
                foreach (var option in TerritoryOptions)
                {
                    selectedTerritoryIds.Add(option.ID);
                }
            }
            else
            {
                selectedTerritoryIds.Clear();
            }

            changed = true;
        }

        OmniControls.TableHeader(OmniLoc.Get("Common.TerritorySelector.Column.Map"));
        OmniControls.TableHeader(OmniLoc.Get("Common.TerritorySelector.Column.Region"));
        OmniControls.TableHeader(OmniLoc.Get("Common.TerritorySelector.Column.Id"));

        foreach (var option in rows)
        {
            ImGui.PushID((int)option.ID);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            OmniControls.CenterTableItem(
                new Vector2(OmniTheme.CheckboxSize()),
                OmniTheme.SmallButtonSize().Y);
            var selected = selectedTerritoryIds.Contains(option.ID);
            if (OmniControls.Checkbox($"##{id}Selected", ref selected))
            {
                changed |= selected
                    ? selectedTerritoryIds.Add(option.ID)
                    : selectedTerritoryIds.Remove(option.ID);
            }

            ImGui.TableNextColumn();
            OmniControls.TableTextCentered(option.Name, OmniTheme.SmallButtonSize().Y);
            ImGui.TableNextColumn();
            OmniControls.TableTextCentered(
                option.Region,
                OmniTheme.SmallButtonSize().Y,
                ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TableNextColumn();
            OmniControls.TableTextCentered(option.ID.ToString(), OmniTheme.SmallButtonSize().Y);
            ImGui.PopID();
        }

        return changed;
    }

    private IReadOnlyList<TerritoryOption> GetVisibleTerritories(
        HashSet<uint> selectedTerritoryIds,
        string query)
    {
        visibleTerritoryOptions.Clear();
        var exactID = uint.TryParse(query, out var parsedId) ? parsedId : 0;
        foreach (var option in TerritoryOptions)
        {
            if (exactID != 0
                ? option.ID != exactID
                : query.Length > 0 && !option.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            visibleTerritoryOptions.Add(option);
        }

        visibleTerritoryOptions.Sort((left, right) =>
        {
            var selectedComparison = selectedTerritoryIds.Contains(right.ID)
                .CompareTo(selectedTerritoryIds.Contains(left.ID));
            return selectedComparison != 0 ? selectedComparison : left.ID.CompareTo(right.ID);
        });
        return visibleTerritoryOptions;
    }

    private static void EnsureTerritoryOptions()
    {
        if (TerritoryOptionsLoaded)
        {
            return;
        }

        TerritoryOptionsLoaded = true;
        foreach (var territory in LuminaGetter.Get<TerritoryType>())
        {
            if (territory.RowId == 0)
            {
                continue;
            }

            var name = territory.PlaceName.ValueNullable?.Name.ToString().Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = territory.Name.ToString().Trim();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var regionName = territory.PlaceNameRegion.ValueNullable?.Name.ToString() ?? string.Empty;
            var zoneName = territory.PlaceNameZone.ValueNullable?.Name.ToString() ?? string.Empty;
            var region = BuildRegionText(regionName, zoneName);
            TerritoryOptions.Add(new(
                territory.RowId,
                name,
                region,
                OmniSearchText.Build(
                    territory.RowId.ToString(),
                    name,
                    territory.Name.ToString(),
                    region,
                    regionName,
                    zoneName,
                    territory.ContentFinderCondition.ValueNullable?.Name.ToString() ?? string.Empty)));
            TerritoryOptionIDs.Add(territory.RowId);
        }

        TerritoryOptions.Sort(static (left, right) => left.ID.CompareTo(right.ID));
    }

    private static string GetTerritoryName(uint territoryID)
    {
        foreach (var option in TerritoryOptions)
        {
            if (option.ID == territoryID)
            {
                return option.Name;
            }
        }

        return string.Format(OmniLoc.Get("Common.TerritorySelector.Fallback"), territoryID);
    }

    private static string BuildRegionText(string regionName, string zoneName)
    {
        regionName = regionName.Trim();
        zoneName = zoneName.Trim();
        if (regionName.Length == 0)
        {
            return zoneName;
        }

        return zoneName.Length == 0 || string.Equals(regionName, zoneName, StringComparison.Ordinal)
            ? regionName
            : $"{regionName} / {zoneName}";
    }

    private readonly record struct TerritoryOption(uint ID, string Name, string Region, string SearchText);
}
