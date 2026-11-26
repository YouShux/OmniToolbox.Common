namespace OmniToolbox.UI.Controls;

public readonly record struct ItemSelectionTableChange
(
    ItemSelectionTableAction Action,
    uint                     ItemID = 0,
    bool                     Enabled = false
);
