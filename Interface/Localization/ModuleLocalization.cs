using TreeTools.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Linq;

namespace OmniToolbox.UI;

/// <summary>
/// 随模块交付的界面词条。作者提供简体中文，繁体中文由 Omni 按访问转换并缓存。
/// </summary>
public sealed class ModuleLocalization
{
    private FrozenDictionary<string, string> simplified = null!;
    private IReadOnlyDictionary<string, string> traditional = null!;

    /// <summary>
    /// 获取模块的简体中文词条，必须包含非空的 Title、Description 及模块使用的显示词条。
    /// </summary>
    public required IReadOnlyDictionary<string, string> SimplifiedChinese
    {
        get => simplified;
        init
        {
            simplified = XliffResourceParser.CopyTexts(value);
            foreach (var key in new[] { "Title", "Description" })
                if (!simplified.ContainsKey(key))
                    throw new InvalidDataException($"Module localization requires a nonempty {key} entry.");
            traditional = new GeneratedTraditional(simplified);
        }
    }

    /// <summary>
    /// 获取与简体词条具有相同键集的只读繁体词条。作者无需填写繁体译文。
    /// </summary>
    public IReadOnlyDictionary<string, string> TraditionalChinese => traditional;

    /// <summary>
    /// 通过明确的模块键与官方资源 ID 对应表创建词条，不保存格式化后的显示文本。
    /// </summary>
    public static ModuleLocalization FromResources(IReadOnlyDictionary<string, string> resourceKeys)
    {
        ArgumentNullException.ThrowIfNull(resourceKeys);
        return new()
        {
            SimplifiedChinese = resourceKeys.ToDictionary(static pair => pair.Key,
                static pair => OmniLoc.GetOfficialSource(pair.Value), StringComparer.Ordinal)
        };
    }

    private sealed class GeneratedTraditional(FrozenDictionary<string, string> source) : IReadOnlyDictionary<string, string>
    {
        private readonly ConcurrentDictionary<string, string> converted = new(StringComparer.Ordinal);

        public int Count => source.Count;
        public IEnumerable<string> Keys => source.Keys;
        public IEnumerable<string> Values => Keys.Select(key => this[key]);
        public string this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

        public bool ContainsKey(string key) => source.ContainsKey(key);

        public bool TryGetValue(string key, out string value)
        {
            if (!source.TryGetValue(key, out var text))
            {
                value = string.Empty;
                return false;
            }
            value = converted.GetOrAdd(key, static (_, original) => ChineseTextConverter.ToTraditional(original), text);
            return true;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            Keys.Select(key => KeyValuePair.Create(key, this[key])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
