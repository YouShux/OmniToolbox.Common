namespace OmniToolbox.Config;

public static class UILanguages
{
    public static IReadOnlyList<UILanguage> All { get; } = Array.AsReadOnly(Enum.GetValues<UILanguage>());

    public static string GetCode(UILanguage language) => language switch
    {
        UILanguage.SimplifiedChinese => "zh-CN",
        UILanguage.TraditionalChinese => "zh-TW",
        UILanguage.English => "en",
        UILanguage.Japanese => "ja",
        UILanguage.German => "de",
        UILanguage.French => "fr",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
    };

    public static string GetResourceName(UILanguage language) => language switch
    {
        UILanguage.SimplifiedChinese => "ChineseSimplified.xlf",
        UILanguage.TraditionalChinese => "TraditionalChinese.xlf",
        UILanguage.English => "English.xlf",
        UILanguage.Japanese => "Japanese.xlf",
        UILanguage.German => "German.xlf",
        UILanguage.French => "French.xlf",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
    };

    public static string GetNameKey(UILanguage language) => language switch
    {
        UILanguage.SimplifiedChinese => "Settings.Ui.Language.Simplified",
        UILanguage.TraditionalChinese => "Settings.Ui.Language.Traditional",
        UILanguage.English => "Settings.Ui.Language.English",
        UILanguage.Japanese => "Settings.Ui.Language.Japanese",
        UILanguage.German => "Settings.Ui.Language.German",
        UILanguage.French => "Settings.Ui.Language.French",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
    };
}
