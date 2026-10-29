using OmniToolbox.Items;

namespace OmniToolbox.Collections;

public readonly record struct BlueSpellSource
(
    string SourceType,
    string MobDescription,
    string LocationDescription,
    float? X,
    float? Y,
    uint?  Level,
    string Note
)
{
    internal uint DutyID { get; init; }

    internal uint TerritoryID { get; init; }
}
