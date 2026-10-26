using System.Collections.Frozen;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

internal static class XliffResourceParser
{
    private static readonly Regex PlaceholderPattern = new(@"\{\d+(?:[^{}]*)\}", RegexOptions.CultureInvariant);

    internal static FrozenDictionary<string, string> Parse(Stream stream, UILanguage language = UILanguage.SimplifiedChinese)
    {
        var code = UILanguages.GetCode(language);
        var required = language is UILanguage.SimplifiedChinese or UILanguage.TraditionalChinese;
        using var reader = XmlReader.Create(stream, new()
        {
            CloseInput = false,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            XmlResolver = null
        });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XNamespace xmlNamespace = "urn:oasis:names:tc:xliff:document:1.2";
        if (document.Root?.Name != xmlNamespace + "xliff" || document.Root.Attribute("version")?.Value != "1.2")
            throw new XmlException("Expected an XLIFF 1.2 document.");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = document.Root.Elements(xmlNamespace + "file").ToArray();
        if (files.Length == 0)
            throw new XmlException("XLIFF requires a file element.");
        foreach (var file in files)
        {
            var targetLanguage = file.Attribute("target-language")?.Value;
            if (file.Attribute("source-language")?.Value != UILanguages.GetCode(UILanguage.SimplifiedChinese) ||
                (language == UILanguage.SimplifiedChinese ? targetLanguage is not null : targetLanguage != code))
                throw new XmlException("XLIFF source or target language does not match the resource role.");
            var valueName = xmlNamespace + (language == UILanguage.SimplifiedChinese ? "source" : "target");
            foreach (var entry in file.Descendants(xmlNamespace + "trans-unit"))
            {
                var key = entry.Attribute("id")?.Value;
                if (string.IsNullOrWhiteSpace(key))
                    throw new XmlException("XLIFF translation ID is missing.");
                if (result.ContainsKey(key))
                    throw new XmlException($"Duplicate XLIFF translation ID: {key}");
                var value = entry.Element(valueName)?.Value;
                if (required && string.IsNullOrWhiteSpace(value))
                    throw new XmlException($"XLIFF translation is missing or empty: {key}");
                result.Add(key, value ?? string.Empty);
            }
        }
        return CopyTexts(result, !required);
    }

    internal static FrozenDictionary<string, string> CopyTexts(IReadOnlyDictionary<string, string> texts, bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (!allowEmpty && texts.Count == 0)
            throw new InvalidDataException("Localization catalog is empty.");
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in texts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (value is null || !allowEmpty && string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"Localization text is empty: {key}");
            try
            {
                XmlConvert.VerifyXmlChars(key);
                XmlConvert.VerifyXmlChars(value);
            }
            catch (XmlException exception)
            {
                throw new InvalidDataException($"Localization text contains invalid characters: {key}", exception);
            }
            copy.Add(key, value);
        }
        return copy.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static void ValidateTranslation(IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> translation, bool requireComplete)
    {
        if (translation.Keys.Any(key => !source.ContainsKey(key)))
            throw new InvalidDataException("Localization translation contains an unknown source key.");
        if (requireComplete && (source.Count != translation.Count || translation.Values.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException("Required localization keys must match completely and contain nonempty text.");
        foreach (var (key, text) in translation)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var sourcePlaceholders = PlaceholderPattern.Matches(source[key]).Select(static match => match.Value).Order(StringComparer.Ordinal);
            var targetPlaceholders = PlaceholderPattern.Matches(text).Select(static match => match.Value).Order(StringComparer.Ordinal);
            if (!sourcePlaceholders.SequenceEqual(targetPlaceholders, StringComparer.Ordinal))
                throw new InvalidDataException($"Localization placeholders do not match: {key}");
        }
    }
}
