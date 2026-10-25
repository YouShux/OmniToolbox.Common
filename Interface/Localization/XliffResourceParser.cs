using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OmniToolbox.UI;

internal static class XliffResourceParser
{
    private static readonly Regex PlaceholderPattern = new(@"\{\d+(?:[^{}]*)\}", RegexOptions.CultureInvariant);

    internal static FrozenDictionary<string, string> Parse(Stream stream, bool translation = false)
    {
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
        foreach (var file in document.Root.Elements(xmlNamespace + "file"))
        {
            var targetLanguage = file.Attribute("target-language")?.Value;
            if (translation ? targetLanguage != "zh-TW" : targetLanguage is not null)
                throw new XmlException("XLIFF source or target language does not match the resource role.");
            var valueName = xmlNamespace + (translation ? "target" : "source");
            foreach (var entry in file.Descendants(xmlNamespace + "trans-unit"))
            {
                var key = entry.Attribute("id")?.Value;
                if (string.IsNullOrWhiteSpace(key))
                    throw new XmlException("XLIFF translation ID is missing.");
                if (result.ContainsKey(key))
                    throw new XmlException($"Duplicate XLIFF translation ID: {key}");
                var value = entry.Element(valueName)?.Value;
                if (string.IsNullOrWhiteSpace(value))
                    throw new XmlException($"XLIFF translation is missing or empty: {key}");
                result.Add(key, value);
            }
        }
        return CopyTexts(result);
    }

    internal static FrozenDictionary<string, string> CopyTexts(IReadOnlyDictionary<string, string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
            throw new InvalidDataException("Localization catalog is empty.");
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in texts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
                throw new InvalidDataException($"Localization text is empty or contains a null character: {key}");
            copy.Add(key, value);
        }
        return copy.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static void ValidatePair(IReadOnlyDictionary<string, string> simplified, IReadOnlyDictionary<string, string> traditional)
    {
        if (simplified.Count != traditional.Count || simplified.Keys.Any(key => !traditional.ContainsKey(key)))
            throw new InvalidDataException("Simplified and Traditional localization keys must match completely.");
        foreach (var (key, source) in simplified)
        {
            var sourcePlaceholders = PlaceholderPattern.Matches(source).Select(static match => match.Value).Order(StringComparer.Ordinal);
            var targetPlaceholders = PlaceholderPattern.Matches(traditional[key]).Select(static match => match.Value).Order(StringComparer.Ordinal);
            if (!sourcePlaceholders.SequenceEqual(targetPlaceholders, StringComparer.Ordinal))
                throw new InvalidDataException($"Traditional localization placeholders do not match: {key}");
        }
    }
}
