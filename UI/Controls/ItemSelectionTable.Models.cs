namespace OmniToolbox.UI.Controls;

public enum ItemSelectionTableAction
{
    None,
    SetAll,
    SetItem,
    Delete
}

public readonly record struct ItemSelectionTableChange
(
    ItemSelectionTableAction Action,
    uint                     ItemID = 0,
    bool                     Enabled = false
);

public readonly record struct ItemSelectionTableRow
(
    uint ItemID,
    bool Enabled,
    bool CanDelete
);
