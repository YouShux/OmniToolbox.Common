namespace OmniToolbox.Items;

public readonly record struct ItemSearchResult
(
    uint             ID,
    string           Name,
    uint             IconID,
    uint             Level,
    string           Category,
    uint             ItemUICategoryID,
    bool             HasCategory,
    string           ItemSearchCategory,
    uint             ItemSearchCategoryID,
    uint             ItemSearchCategoryIconID,
    ItemCategory     MainCategory,
    bool             IsTradable,
    bool             IsCurrency,
    bool             CanBeHQ,
    byte             DyeCount,
    ItemMainModelKey MainModel,
    ulong            JobMask,
    decimal          Patch
);
