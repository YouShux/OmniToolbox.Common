using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    /// <remarks>必须在 Framework 线程调用，以读取客户端文本。</remarks>
    public IReadOnlyList<CollectionSource> GetOrchestrionSources(uint orchestrionID)
    {
        if (!LuminaGetter.TryGetRow<Orchestrion>(orchestrionID, out var orchestrion) ||
            orchestrion.Name.IsEmpty ||
            !LuminaGetter.TryGetRow<OrchestrionUiparam>(orchestrionID, out var parameters) ||
            parameters.OrchestrionCategory.RowId != 8)
        {
            return [];
        }

        var product = orchestrionID switch
        {
            278 => "FINAL FANTASY XIV - The Best Compilation Album",
            279 => "THE PRIMALS Zepp Tour 2018 - Trial By Shadow",
            305 or 306 => "Journeys: FINAL FANTASY XIV Arrangement Album",
            406 => "SCIONS & SINNERS: FINAL FANTASY XIV Arrangement Album",
            562 or 563 => "THE PRIMALS Live in Japan - Beyond the Shadow",
            589 => "Eorzean Symphony: FINAL FANTASY XIV Orchestral Album Vol. 3",
            641 or 642 => "Forge Ahead: FINAL FANTASY XIV Arrangement Album",
            691 => "And Back Again: Live Performances from the FINAL FANTASY XIV Fan Festival 2024",
            725 or 726 => "THE PRIMALS Live in Japan - Darkest Before Dawn",
            831 or 832 => "THE PRIMALS Dark Decades Tour BUDOKAN",
            889 or 890 => "Eorzean Symphony: FINAL FANTASY XIV Orchestral Album Vol. 4",
            _ => string.Empty
        };
        return product.Length == 0
            ? []
            :
            [
                new(
                    CollectionSourceCategory.Other,
                    0,
                    string.Format(OmniLoc.Get("Collection.Source.Orchestrion.MusicBonus"), product),
                    OmniLoc.Get("Collection.Source.Orchestrion.MusicBonusDetail"))
            ];
    }
}
