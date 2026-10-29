namespace OmniToolbox.Items;

public readonly record struct ItemSetInfo
(
    ItemSearchResult                SetItem,
    IReadOnlyList<ItemSearchResult> Items
);
