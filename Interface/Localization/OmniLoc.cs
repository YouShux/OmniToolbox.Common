using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using OmniToolbox.Collections;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static partial class OmniLoc
{
    private static readonly Dictionary<string, string> Texts = BuildTexts();
    private static Lazy<IReadOnlyDictionary<string, string>> TraditionalTexts = new(BuildTraditionalTexts);
    private const uint TRADITIONAL_CHINESE_MAP_FLAG = 0x04000000;
    private static UILanguage Language;

    public static void RegisterTexts(IReadOnlyDictionary<string, string> texts)
    {
        AddTexts(Texts, texts);
        TraditionalTexts = new(BuildTraditionalTexts);
    }

    public static void SetLanguage(UILanguage value) => Language = value;

    public static string Get(string key)
    {
        if (!Texts.TryGetValue(key, out var text))
        {
            return key;
        }

        return Language == UILanguage.TraditionalChinese
            ? TraditionalTexts.Value[key]
            : text;
    }

    public static string Get(ItemAvailability availability) => Get(availability switch
    {
        ItemAvailability.Obtainable => "Status.Obtainable",
        ItemAvailability.Purchasable => "Status.Purchasable",
        ItemAvailability.Unobtainable => "Status.Unobtainable",
        _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null)
    });

    private static Dictionary<string, string> BuildTexts()
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        AddTexts(texts, CreateCommonTexts());
        return texts;
    }

    private static void AddTexts(
        Dictionary<string, string> texts,
        IReadOnlyDictionary<string, string> additions)
    {
        foreach (var (key, value) in additions)
        {
            if (!texts.TryAdd(key, value))
            {
                throw new InvalidOperationException($"Duplicate localization key: {key}");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> BuildTraditionalTexts()
        => Texts.ToDictionary(
            static entry => entry.Key,
            static entry => ConvertToTraditional(entry.Value),
            StringComparer.Ordinal);

    private static string ConvertToTraditional(string text)
    {
        var length = LCMapStringEx(
            "zh-TW",
            TRADITIONAL_CHINESE_MAP_FLAG,
            text,
            -1,
            null,
            0,
            nint.Zero,
            nint.Zero,
            nint.Zero);
        if (length <= 1)
        {
            return text;
        }

        var result = new StringBuilder(length);
        return LCMapStringEx(
                   "zh-TW",
                   TRADITIONAL_CHINESE_MAP_FLAG,
                   text,
                   -1,
                   result,
                   length,
                   nint.Zero,
                   nint.Zero,
                   nint.Zero) == length
            ? result.ToString()
            : text;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int LCMapStringEx(
        string localeName,
        uint mapFlags,
        string source,
        int sourceLength,
        StringBuilder? destination,
        int destinationLength,
        nint versionInformation,
        nint reserved,
        nint sortHandle);
}
