using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public readonly record struct ItemSelectionTableRow
(
    uint ItemID,
    bool Enabled,
    bool CanDelete
);
