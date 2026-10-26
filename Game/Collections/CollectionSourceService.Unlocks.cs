using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    public static string GetItemSourceNote(uint itemID) => itemID switch
    {
        20562 or 21034 or 41504 or 47939 or 47940 or 52281 or >= 52413 and <= 52417 or 52439 or 52447 =>
            OmniLoc.Get("Collection.Source.Unlock.NotVerified"),
        _ => string.Empty
    };

    private static void AddPromotionalUnlockSources(IDictionary<uint, HashSet<CollectionSource>> itemRows)
    {
        AddSourceIfMissing(itemRows, 41498, new(
            CollectionSourceCategory.Event,
            0,
            OmniLoc.Get("Collection.Source.Unlock.Alrsvid"),
            OmniLoc.Get("Collection.Source.Unlock.AlrsvidDetail")));
        AddSourceIfMissing(itemRows, 52654, new(
            CollectionSourceCategory.Other,
            0,
            OmniLoc.Get("Collection.Source.Unlock.MusicalPaca"),
            OmniLoc.Get("Collection.Source.Unlock.MusicalPacaDetail")));
        AddSourceIfMissing(itemRows, 52638, new(
            CollectionSourceCategory.Other,
            0,
            OmniLoc.Get("Collection.Source.Unlock.LollyCat"),
            OmniLoc.Get("Collection.Source.Unlock.LollyCatDetail")));
        AddSourceIfMissing(itemRows, 50021, new(
            CollectionSourceCategory.Event,
            0,
            OmniLoc.Get("Collection.Source.Unlock.LaudedFramersKit"),
            OmniLoc.Get("Collection.Source.Unlock.LaudedFramersKitDetail")));
    }

    /// <remarks>必须在 Framework 线程调用，以读取客户端文本。</remarks>
    public IReadOnlyList<CollectionSource> GetMountSources(uint mountID) =>
        mountID == 50 ? BuildQuestUnlockSources(67204) : [];

    /// <remarks>必须在 Framework 线程调用，以读取客户端文本。</remarks>
    public IReadOnlyList<CollectionSource> GetCompanionSources(uint companionID) =>
        companionID == 119 ? BuildQuestUnlockSources(65902) : [];

    /// <remarks>必须在 Framework 线程调用，以读取客户端文本。</remarks>
    public IReadOnlyList<CollectionSource> GetBardingSources(uint bardingID)
    {
        if (bardingID is 1 or 5 or 9 &&
            LuminaGetter.TryGetRow<BuddyEquip>(bardingID, out var barding) &&
            barding.GrandCompany.ValueNullable is { } company)
        {
            return
            [
                new(
                    CollectionSourceCategory.Other,
                    0,
                    string.Format(OmniLoc.Get("Collection.Source.Barding.JoinCompany"), company.Name.ExtractText()),
                    OmniLoc.Get("Collection.Source.Barding.CompanyUnlock"))
            ];
        }

        var detailKey = bardingID switch
        {
            13 => "Collection.Source.Barding.Defender",
            14 => "Collection.Source.Barding.Attacker",
            15 => "Collection.Source.Barding.Healer",
            _ => string.Empty
        };
        return detailKey.Length != 0
            ? [new(CollectionSourceCategory.Other, 0, OmniLoc.Get("Collection.Source.Barding.Training"), OmniLoc.Get(detailKey))]
            : [];
    }

    /// <remarks>必须在 Framework 线程调用，以读取客户端文本。</remarks>
    public IReadOnlyList<CollectionSource> GetEmoteSources(uint emoteID)
    {
        if (!LuminaGetter.TryGetRow<Emote>(emoteID, out var emote))
            return [];

        switch (emote.UnlockLink)
        {
            case 229:
                return BuildQuestUnlockSources(67114, OmniLoc.Get("Collection.Source.Unlock.EternalBond"));
            case 340:
                return BuildQuestUnlockSources(68020);
            case 373 when LuminaGetter.TryGetRow<Achievement>(1966, out var achievement):
                return
                [
                    new(
                        CollectionSourceCategory.Achievement,
                        achievement.RowId,
                        achievement.Name.ExtractText(),
                        achievement.Description.ExtractText())
                ];
            default:
                return [];
        }
    }

    private static IReadOnlyList<CollectionSource> BuildQuestUnlockSources(uint questID, string detail = "") =>
        LuminaGetter.TryGetRow<Quest>(questID, out var quest)
            ? [new(CollectionSourceCategory.Quest, questID, quest.Name.ExtractText(), detail)]
            : [];
}
