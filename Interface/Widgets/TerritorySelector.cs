using System.Globalization;
using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;
using TreeToolKit;
using TreeTools.Text;

namespace OmniToolbox.UI.Controls;

public sealed class TerritorySelector
{
    private static readonly List<TerritoryOption> TerritoryOptions       = [];
    private static readonly HashSet<uint>         TerritoryOptionIDs     = [];
    private static bool                           TerritoryOptionsLoaded;

    private readonly string                                                                                 id;
    private readonly string searchID;
    private readonly string tableID;
    private readonly string checkColumnID;
    private readonly string selectAllID;
    private readonly string selectedID;
    private (int Count, long Localization, string Culture)? headerKey;
    private (uint Territory, long Localization, string Culture)? currentTerritoryKey;
    private string headerLabel = string.Empty;
    private string addCurrentLabel = string.Empty;
    private string currentTerritoryText = string.Empty;
    private readonly List<TerritoryOption>                                                                  visibleTerritoryOptions = [];
    private string                                                                                          search                  = string.Empty;
    private string?                                                                                         visibleQuery;
    private readonly HashSet<uint>                                                                          visibleSelection        = [];
    private (float MapWidth, float RegionWidth, float BaseHeight, float FontSize, nint Font, float Height)? estimatedLayout;
    private (float MapWidth, float RegionWidth, float BaseHeight, float FontSize, nint Font, float Height)? actualLayout;
    private long fontRevision = -1;
    private long localizationRevision = -1;
    private OmniTableRowLayout[] detailHeights = [];
    private (float Width, nint Font, float FontSize, Vector2 Padding, Vector2 Spacing, Vector2 FramePadding,
        Vector2 InnerSpacing, float RowHeight)? detailLayoutKey;

    public TerritorySelector(string id)
    {
        this.id = id;
        searchID = $"##{id}Search";
        tableID = $"##{id}Table";
        checkColumnID = $"##{id}Check";
        selectAllID = $"##{id}SelectAll";
        selectedID = $"##{id}Selected";
    }

    public bool Draw
    (
        HashSet<uint> selectedTerritoryIds,
        string        emptyText
    )
    {
        EnsureTerritoryOptions();
        var changed = TerritoryOptionIDs.Count > 0 &&
                      selectedTerritoryIds.RemoveWhere(territoryID => !TerritoryOptionIDs.Contains(territoryID)) > 0;
        var displayHeaderKey = (selectedTerritoryIds.Count, OmniLoc.Revision, CultureInfo.CurrentCulture.Name);
        if (headerKey != displayHeaderKey)
        {
            headerKey = displayHeaderKey;
            var header = string.Format(OmniLoc.Get("Common.TerritorySelector.Title"), selectedTerritoryIds.Count);
            headerLabel = $"{header}##{id}Header";
            addCurrentLabel = $"{OmniLoc.Get("Common.TerritorySelector.AddCurrent")}##{id}AddCurrent";
        }
        if (!OmniControls.CollapsingHeader(
                headerLabel,
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
                    addCurrentLabel,
                    false))
            {
                changed |= selectedTerritoryIds.Add(currentTerritoryID);
            }
        }

        var displayTerritoryKey = (currentTerritoryID, OmniLoc.Revision, CultureInfo.CurrentCulture.Name);
        if (currentTerritoryKey != displayTerritoryKey)
        {
            currentTerritoryKey = displayTerritoryKey;
            currentTerritoryText = currentTerritoryID == 0
                ? OmniLoc.Get("Common.TerritorySelector.LoggedOut")
                : string.Format(OmniLoc.Get("Common.TerritorySelector.Current"), GetTerritoryName(currentTerritoryID), currentTerritoryID);
        }
        OmniControls.SameLineOrWrap(OmniTextMeasurements.Measure(currentTerritoryText).X);
        ImGui.AlignTextToFramePadding();
        using (ImRaii.TextWrapPos(0f))
            ImGui.TextDisabled(currentTerritoryText);

        OmniControls.InputTextWithHint(
            searchID,
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

    private unsafe bool DrawTable(HashSet<uint> selectedTerritoryIds)
    {
        if (fontRevision != Fonts.Revision || localizationRevision != OmniLoc.Revision)
        {
            fontRevision = Fonts.Revision;
            localizationRevision = OmniLoc.Revision;
            estimatedLayout = actualLayout = null;
            detailLayoutKey = null;
        }
        var rows = GetVisibleTerritories(selectedTerritoryIds, search.Trim());
        if (rows.Count == 0)
        {
            ImGui.TextDisabled(OmniLoc.Get("Common.TerritorySelector.NoMatches"));
            return false;
        }

        var available = ImGui.GetContentRegionAvail();
        var checkWidth = OmniControls.MeasureCheckbox(string.Empty).X;
        var idWidth = OmniControls.MeasureInput("65535", OmniTheme.Scale(74f)).X;
        var nameWidth = (available.X - checkWidth - idWidth - ImGui.GetStyle().CellPadding.X * 8f - ImGui.GetStyle().ScrollbarSize) / 2.35f;
        var detailLayout = nameWidth < ImGui.GetFontSize() * 4f;
        var rowHeight = MathF.Max(ImGui.GetFrameHeight(), checkWidth);
        if (!detailLayout)
        {
            rowHeight = MeasureRowHeight(MathF.Max(1f, nameWidth * 1.35f), MathF.Max(1f, nameWidth), rowHeight, false);
        }
        var headerHeight = MathF.Max(OmniTheme.SmallButtonSize().Y, ImGui.GetFrameHeight());
        var tableHeight = MathF.Ceiling(headerHeight + ImGui.GetStyle().CellPadding.Y * 2f +
            (rowHeight + ImGui.GetStyle().CellPadding.Y * 2f) * Math.Min(rows.Count, 6) +
            OmniTheme.BorderThickness() * 2f);
        using var table = ImRaii.Table(
            tableID,
            detailLayout ? 1 : 4,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingStretchProp,
            new Vector2(0f, tableHeight));
        if (!table)
        {
            return false;
        }

        if (!detailLayout)
            ImGui.TableSetupColumn(checkColumnID, ImGuiTableColumnFlags.WidthFixed, checkWidth);
        ImGui.TableSetupColumn(
            OmniLoc.Get("Common.TerritorySelector.Column.Map"),
            ImGuiTableColumnFlags.WidthStretch,
            1.35f);
        if (!detailLayout)
        {
            ImGui.TableSetupColumn(
                OmniLoc.Get("Common.TerritorySelector.Column.Region"),
                ImGuiTableColumnFlags.WidthStretch,
                1f);
            ImGui.TableSetupColumn(
                OmniLoc.Get("Common.TerritorySelector.Column.Id"),
                ImGuiTableColumnFlags.WidthFixed,
                idWidth);
        }
        ImGui.TableSetupScrollFreeze(0, 1);
        OmniControls.BeginTableHeaderRow();
        ImGui.TableNextColumn();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));
        var selectAll = TerritoryOptions.Count > 0 && selectedTerritoryIds.Count == TerritoryOptions.Count;
        if (!detailLayout)
            OmniControls.CenterTableItem(new Vector2(checkWidth), OmniTheme.SmallButtonSize().Y);
        var changed = false;
        if (OmniControls.Checkbox(selectAllID, ref selectAll))
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

        if (detailLayout)
        {
            OmniControls.SameLineOrWrap(OmniTextMeasurements.Measure(OmniLoc.Get("Common.TerritorySelector.Column.Map")).X);
            ImGui.TextUnformatted(OmniLoc.Get("Common.TerritorySelector.Column.Map"));
        }
        else
        {
            OmniControls.TableHeader(OmniLoc.Get("Common.TerritorySelector.Column.Map"));
            OmniControls.TableHeader(OmniLoc.Get("Common.TerritorySelector.Column.Region"));
            OmniControls.TableHeader(OmniLoc.Get("Common.TerritorySelector.Column.Id"));
            ImGui.TableSetColumnIndex(1);
            var mapWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
            ImGui.TableSetColumnIndex(2);
            var regionWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
            rowHeight = MathF.Max(ImGui.GetFrameHeight(), checkWidth);
            rowHeight = MeasureRowHeight(mapWidth, regionWidth, rowHeight, true);
        }

        var clipper = ImGui.ImGuiListClipper();
        if (!detailLayout)
            clipper.Begin(rows.Count, rowHeight + ImGui.GetStyle().CellPadding.Y * 2f);
        var detailKey = (available.X, (nint)ImGui.GetFont().Handle, ImGui.GetFontSize(), ImGui.GetStyle().CellPadding, ImGui.GetStyle().ItemSpacing,
            ImGui.GetStyle().FramePadding, ImGui.GetStyle().ItemInnerSpacing, rowHeight);
        var measuredDetails = detailLayoutKey == detailKey && detailHeights.Length == rows.Count;
        if (!measuredDetails)
        {
            detailLayoutKey = detailKey;
            detailHeights = new OmniTableRowLayout[rows.Count];
        }
        var interaction = new OmniListInteraction("##territoryRows");
        if (!detailLayout)
        {
            if (interaction.RetainAll)
                clipper.ForceDisplayRangeByIndices(0, rows.Count);
            else if (interaction.HasRetainedRows)
                for (var index = 0; index < rows.Count; index++)
                    interaction.IncludeRetainedRow(clipper, index, rows[index].ID);
        }
        while (detailLayout || clipper.Step())
        {
            var first = detailLayout ? 0 : clipper.DisplayStart;
            var last = detailLayout ? rows.Count : clipper.DisplayEnd;
            for (var rowIndex = first; rowIndex < last; rowIndex++)
            {
                var option = rows[rowIndex];

                ref var rowLayout = ref detailHeights[rowIndex];
                if (!rowLayout.BeginRow(rowHeight, detailLayout ? 4 : 1,
                        !detailLayout || interaction.ShouldRetain(option.ID)))
                {
                    continue;
                }
                ImGui.TableNextColumn();
                using var rowScope = interaction.BeginRow(option.ID);
                using var idScope = ImRaii.PushId((int)option.ID);
                if (!detailLayout)
                    OmniControls.CenterTableItem(new Vector2(checkWidth), rowHeight);
                var selected = selectedTerritoryIds.Contains(option.ID);
                if (OmniControls.Checkbox(selectedID, ref selected))
                {
                    changed |= selected
                        ? selectedTerritoryIds.Add(option.ID)
                        : selectedTerritoryIds.Remove(option.ID);
                }

                rowLayout.NextField(OmniLoc.Get("Common.TerritorySelector.Column.Map"), detailLayout);
                OmniControls.TableTextCentered(option.Name, detailLayout ? 0f : rowHeight);
                rowLayout.NextField(OmniLoc.Get("Common.TerritorySelector.Column.Region"), detailLayout);
                OmniControls.TableTextCentered(
                    option.Region,
                    detailLayout ? 0f : rowHeight,
                    ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                rowLayout.NextField(OmniLoc.Get("Common.TerritorySelector.Column.Id"), detailLayout);
                OmniControls.TableTextCentered(option.IDText, detailLayout ? 0f : rowHeight);
                rowLayout.EndRow();
            }
            if (detailLayout)
                break;
        }
        clipper.Destroy();

        return changed;
    }

    private IReadOnlyList<TerritoryOption> GetVisibleTerritories
    (
        HashSet<uint> selectedTerritoryIds,
        string        query
    )
    {
        if (visibleQuery == query && visibleSelection.SetEquals(selectedTerritoryIds))
            return visibleTerritoryOptions;

        visibleQuery = query;
        visibleSelection.Clear();
        visibleSelection.UnionWith(selectedTerritoryIds);
        estimatedLayout = actualLayout = null;
        detailLayoutKey = null;
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

    private unsafe float MeasureRowHeight
    (
        float mapWidth,
        float regionWidth,
        float baseHeight,
        bool  actual
    )
    {
        ref var cached = ref (actual ? ref actualLayout : ref estimatedLayout);
        var font = (nint)ImGui.GetFont().Handle;
        var fontSize = ImGui.GetFontSize();
        if (cached is { } layout && layout.MapWidth == mapWidth && layout.RegionWidth == regionWidth &&
            layout.BaseHeight == baseHeight && layout.Font == font && layout.FontSize == fontSize)
            return layout.Height;

        var height = baseHeight;
        foreach (var option in visibleTerritoryOptions)
        {
            height = MathF.Max(height, OmniTextMeasurements.Measure(option.Name, false, mapWidth).Y);
            height = MathF.Max(height, OmniTextMeasurements.Measure(option.Region, false, regionWidth).Y);
        }
        cached = (mapWidth, regionWidth, baseHeight, fontSize, font, height);
        return height;
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
                SearchText.Build(
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

    private static string BuildRegionText
    (
        string regionName,
        string zoneName
    )
    {
        regionName = regionName.Trim();
        zoneName   = zoneName.Trim();
        if (regionName.Length == 0)
        {
            return zoneName;
        }

        return zoneName.Length == 0 || string.Equals(regionName, zoneName, StringComparison.Ordinal)
            ? regionName
            : $"{regionName} / {zoneName}";
    }

    private readonly record struct TerritoryOption
    (
        uint   ID,
        string Name,
        string Region,
        string SearchText
    )
    {
        internal string IDText { get; } = ID.ToString();
    }
}
