using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI.Controls;

public readonly record struct ItemSelectionTableChange(
    ItemSelectionTableAction Action,
    uint ItemID = 0,
    bool Enabled = false);
