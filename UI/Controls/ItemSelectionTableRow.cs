namespace OmniToolbox.UI.Controls;

public readonly record struct ItemSelectionTableRow
(
    uint ItemID,
    bool Enabled,
    bool CanDelete
);
