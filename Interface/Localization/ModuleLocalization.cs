using TreeTools.Text;
using System.IO;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

/// <summary>
/// 随模块交付的冻结词库。简体为源文，繁体自动生成，其他语言可逐步补充。
/// </summary>
public sealed class ModuleLocalization
{
    private readonly OmniLoc.TextSource source;

    private ModuleLocalization(OmniLoc.TextSource source)
    {
        foreach (var key in new[] { "Title", "Description" })
            if (!source.TryGet(key, UILanguage.SimplifiedChinese, out _))
                throw new InvalidDataException($"Module localization requires a nonempty {key} entry.");
        this.source = source;
    }

    public IReadOnlyDictionary<UILanguage, IReadOnlyDictionary<string, string>> Catalogs => source.Catalogs;

    /// <summary>
    /// 从简体源文及可选译文创建词库。繁体由宿主生成，不接受简繁覆盖。
    /// </summary>
    public static ModuleLocalization Create(IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<UILanguage, IReadOnlyDictionary<string, string>>? translations = null)
    {
        var simplified = XliffResourceParser.CopyTexts(source);
        var catalogs = new Dictionary<UILanguage, IReadOnlyDictionary<string, string>>
        {
            [UILanguage.SimplifiedChinese] = simplified,
            [UILanguage.TraditionalChinese] = simplified.ToDictionary(static pair => pair.Key,
                static pair => ChineseTextConverter.ToTraditional(pair.Value), StringComparer.Ordinal)
        };
        if (translations is not null)
            foreach (var (language, texts) in translations)
            {
                if (!Enum.IsDefined(language) || language is UILanguage.SimplifiedChinese or UILanguage.TraditionalChinese)
                    throw new ArgumentOutOfRangeException(nameof(translations), language, "Only additional language translations are accepted.");
                catalogs.Add(language, texts);
            }
        return new(new(catalogs));
    }

    /// <summary>
    /// 将模块键绑定至官方资源 ID，并复制各语言的原始词条。
    /// </summary>
    public static ModuleLocalization FromResources(IReadOnlyDictionary<string, string> resourceKeys) =>
        new(OmniLoc.GetOfficialTexts(resourceKeys));

    public bool IsComplete(UILanguage language) => source.IsComplete(language);

    public bool TryGet(string key, UILanguage language, out string text) => source.TryGet(key, language, out text);

    public string Get(string key, UILanguage language) => TryGet(key, language, out var text)
        ? text : throw new KeyNotFoundException($"Module localization text is missing: {language}/{key}");
}
