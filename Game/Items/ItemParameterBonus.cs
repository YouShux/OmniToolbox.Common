namespace OmniToolbox.Items;

public readonly record struct ItemParameterBonus(
    uint BaseParamID,
    string Name,
    int Value,
    int Max,
    bool IsRelative);
