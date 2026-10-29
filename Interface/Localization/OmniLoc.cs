using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using OmniToolbox.Collections;
using OmniToolbox.Commands;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static class OmniLoc
{
    private static readonly object                                           Sync             = new();
    private static readonly ConcurrentDictionary<string, ModuleRegistration> Modules          = new(StringComparer.Ordinal);
    private static FrozenDictionary<string, TextSource>                      Texts            = new Dictionary<string, TextSource>().ToFrozenDictionary(StringComparer.Ordinal);
    private static TextSource[]                                              OfficialSources;
    private static long                                                      ResourceRevision;
    private static int                                                       Language;

    static OmniLoc()
    {
        var texts = new Dictionary<string, TextSource>(StringComparer.Ordinal);
        OfficialSources = ReadResources(typeof(OmniLoc).Assembly).ToArray();
        foreach (var source in OfficialSources)
            foreach (var key in source.Catalogs[UILanguage.SimplifiedChinese].Keys)
                texts.Add(key, source);
        Texts = texts.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// 获取词条或语言设置发生变化后的递增版本，用于使显示与搜索缓存失效。
    /// </summary>
    public static long Revision => Interlocked.Read(ref ResourceRevision);

    /// <summary>
    /// 获取所有模块共用的当前界面语言。
    /// </summary>
    public static UILanguage CurrentLanguage => (UILanguage)Volatile.Read(ref Language);

    /// <summary>
    /// 在语言、词条注册或模块词条替换完成后通知订阅者。
    /// </summary>
    public static event Action? Changed;

    /// <summary>
    /// 切换共用界面语言，不重新创建或启停模块。
    /// </summary>
    internal static void ApplyLanguage(UILanguage value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, null);
        if (!IsComplete(value))
            throw new InvalidOperationException(Get("Localization.LanguageIncomplete"));
        if (Interlocked.Exchange(ref Language, (int)value) != (int)value)
            NotifyChanged();
    }

    public static bool IsComplete(UILanguage value)
    {
        UILanguages.GetCode(value);
        return OfficialSources.All(source => source.IsComplete(value));
    }

    public static string Get(string key) => Get(key, CurrentLanguage);

    public static string Get
    (
        string     key,
        UILanguage value
    ) => TryGet(key, value, out var text)
        ? text : throw new KeyNotFoundException($"Official localization key is not registered: {key}");

    /// <summary>
    /// 查询模块自带词条。模块之间的同名键相互隔离，缺失词条会明确报错。
    /// </summary>
    public static string Get
    (
        OmniModuleBase owner,
        string         key
    ) => Get(owner, key, CurrentLanguage);

    /// <summary>
    /// 按指定语言查询模块自带词条，缺失词条会明确报错。
    /// </summary>
    public static string Get
    (
        OmniModuleBase owner,
        string         key,
        UILanguage     value
    ) =>
        TryGet(owner, key, value, out var text) ? text
            : throw new KeyNotFoundException($"Module localization key is not registered: {owner.ModuleName}/{key}");

    /// <summary>
    /// 尝试按当前语言获取模块自带词条。
    /// </summary>
    public static bool TryGet
    (
        OmniModuleBase owner,
        string         key,
        out string     text
    ) =>
        TryGet(owner, key, CurrentLanguage, out text);

    /// <summary>
    /// 尝试按指定语言获取模块自带词条。
    /// </summary>
    public static bool TryGet
    (
        OmniModuleBase owner,
        string         key,
        UILanguage     value,
        out string     text
    )
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.LocalizationRegistration is { } registration && (registration.IsReadable || owner.IsDisposing) && registration.Source.TryGet(key, value, out text))
        {
            text = OmniCommandCatalog.FormatDisplayText(text, value);
            return true;
        }
        text = string.Empty;
        return false;
    }

    /// <summary>
    /// 尝试按指定语言获取公共词条，能够区分缺失词条与内容恰好等于键名的词条。
    /// </summary>
    public static bool TryGet
    (
        string     key,
        UILanguage value,
        out string text
    )
    {
        if (Volatile.Read(ref Texts).TryGetValue(key, out var source) && source.TryGet(key, value, out text))
        {
            text = OmniCommandCatalog.FormatDisplayText(text, value);
            return true;
        }
        text = string.Empty;
        return false;
    }

    public static string Get(ItemAvailability availability) => Get(availability switch
    {
        ItemAvailability.Obtainable => "Status.Obtainable",
        ItemAvailability.Purchasable => "Status.Purchasable",
        ItemAvailability.Unobtainable => "Status.Unobtainable",
        _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null)
    });

    /// <summary>
    /// 为候选模块复制并验证词条，不立即改变当前生效词条。
    /// </summary>
    internal static ModuleRegistration PrepareModule(OmniModuleBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var texts = owner.Localization
            ?? throw new InvalidDataException($"Module localization is required: {owner.ModuleName}");
        if (owner.Info.Commands is { } commands)
            foreach (var command in commands)
                if (!texts.Catalogs[UILanguage.SimplifiedChinese].ContainsKey(command.DescriptionKey))
                    throw new InvalidDataException($"Module command localization is missing: {owner.ModuleName}/{command.DescriptionKey}");
        var registration = new ModuleRegistration(owner.ModuleName, new TextSource(texts.Catalogs));
        owner.LocalizationRegistration = registration;
        return registration;
    }

    /// <summary>
    /// 注册正式加载的模块词条，返回由宿主在卸载时释放的登记对象。
    /// </summary>
    internal static ModuleRegistration RegisterModule(OmniModuleBase owner)
    {
        var registration = PrepareModule(owner);
        registration.Activate();
        registration.Commit();
        return registration;
    }

    internal static TextSource GetOfficialTexts(IReadOnlyDictionary<string, string> resourceKeys)
    {
        ArgumentNullException.ThrowIfNull(resourceKeys);
        var catalogs = UILanguages.All.ToDictionary(static language => language,
            static _ => new Dictionary<string, string>(StringComparer.Ordinal));
        var official = Volatile.Read(ref Texts);
        foreach (var (key, resourceID) in resourceKeys)
        {
            if (!official.TryGetValue(resourceID, out var source))
                throw new KeyNotFoundException($"Official localization key is not registered: {resourceID}");
            foreach (var language in UILanguages.All)
                if (source.Catalogs[language].TryGetValue(resourceID, out var text))
                    catalogs[language].Add(key, text);
        }
        return new(catalogs.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyDictionary<string, string>)pair.Value));
    }

    private static IReadOnlyList<TextSource> ReadResources(Assembly assembly) =>
        ReadResources(assembly.GetManifestResourceNames(), name => assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Missing localization resource: {name}"));

    internal static IReadOnlyList<TextSource> ReadResources
    (
        IReadOnlyCollection<string> resourceNames,
        Func<string, Stream>        openResource
    )
    {
        var groups = new Dictionary<string, Dictionary<UILanguage, string>>(StringComparer.Ordinal);
        foreach (var name in resourceNames.Where(static name => name.StartsWith(RESOURCE_PREFIX, StringComparison.Ordinal)))
        {
            var language = UILanguages.All.Where(language => name.EndsWith("." + UILanguages.GetResourceName(language), StringComparison.Ordinal)).ToArray();
            if (language.Length != 1)
                throw new InvalidDataException($"Unexpected localization resource: {name}");
            var prefix = name[..^(UILanguages.GetResourceName(language[0]).Length + 1)];
            if (!groups.TryGetValue(prefix, out var files))
                groups.Add(prefix, files = new());
            files.Add(language[0], name);
        }
        if (groups.Count == 0)
            throw new InvalidDataException("Official localization resources are required.");
        List<TextSource> result = [];
        foreach (var files in groups.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => pair.Value))
        {
            if (!files.ContainsKey(UILanguage.SimplifiedChinese) || !files.ContainsKey(UILanguage.TraditionalChinese))
                throw new InvalidDataException("Simplified and Traditional localization resources are required.");
            var catalogs = new Dictionary<UILanguage, IReadOnlyDictionary<string, string>>();
            foreach (var (language, name) in files)
            {
                using var stream = openResource(name);
                catalogs.Add(language, XliffResourceParser.Parse(stream, language));
            }
            result.Add(new(catalogs));
        }
        return result;
    }

    private static void NotifyChanged()
    {
        Interlocked.Increment(ref ResourceRevision);
        if (Changed is not { } handlers)
            return;
        foreach (var subscriber in handlers.GetInvocationList().Cast<Action>())
        {
            try
            {
                subscriber();
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Localization change subscriber failed: {exception}");
            }
        }
    }

    /// <summary>
    /// 一次模块词条登记。候选激活后可回滚，提交后卸载只撤回当前登记。
    /// </summary>
    internal sealed class ModuleRegistration : IDisposable
    {
        private readonly string     moduleName;
        private ModuleRegistration? previous;
        private volatile bool       activated;
        private volatile bool       disposed;

        internal ModuleRegistration
        (
            string     moduleName,
            TextSource source
        )
        {
            this.moduleName = moduleName;
            Source          = source;
        }

        internal TextSource Source { get; }

        internal bool IsReadable => activated && !disposed;

        /// <summary>
        /// 发布本次登记并保留前一代，便于候选模块启用失败后回滚。
        /// </summary>
        public void Activate()
        {
            bool changed;
            lock (Sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (activated)
                    return;
                previous            = Modules.GetValueOrDefault(moduleName);
                Modules[moduleName] = this;
                activated           = true;
                changed             = true;
            }
            if (changed)
                NotifyChanged();
        }

        /// <summary>
        /// 确认候选替换成功，后续释放不再恢复前一代。
        /// </summary>
        public void Commit()
        {
            lock (Sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (!activated)
                    throw new InvalidOperationException("Module localization must be activated before commit.");
                previous = null;
            }
        }

        /// <summary>
        /// 释放当前登记；旧代次不会撤销已替换它的新登记。
        /// </summary>
        public void Dispose()
        {
            var changed = false;
            lock (Sync)
            {
                if (disposed)
                    return;
                disposed = true;
                if (Modules.TryGetValue(moduleName, out var current) && ReferenceEquals(current, this))
                {
                    changed = true;
                    if (previous is { disposed: false })
                        Modules[moduleName] = previous;
                    else
                        Modules.TryRemove(moduleName, out _);
                }
                previous = null;
            }
            if (changed)
                NotifyChanged();
        }
    }

    internal sealed class TextSource
    {
        internal TextSource(IReadOnlyDictionary<UILanguage, IReadOnlyDictionary<string, string>> catalogs)
        {
            if (catalogs.Keys.Any(language => !Enum.IsDefined(language)) ||
                !catalogs.ContainsKey(UILanguage.SimplifiedChinese) || !catalogs.ContainsKey(UILanguage.TraditionalChinese))
                throw new InvalidDataException("Localization requires Simplified and Traditional source catalogs.");
            var copied = new Dictionary<UILanguage, IReadOnlyDictionary<string, string>>();
            foreach (var language in UILanguages.All)
            {
                var required = language is UILanguage.SimplifiedChinese or UILanguage.TraditionalChinese;
                var texts = catalogs.TryGetValue(language, out var entries) ? entries : FrozenDictionary<string, string>.Empty;
                copied.Add(language, XliffResourceParser.CopyTexts(texts, !required));
            }
            var source = copied[UILanguage.SimplifiedChinese];
            foreach (var language in UILanguages.All.Where(static language => language != UILanguage.SimplifiedChinese))
                XliffResourceParser.ValidateTranslation(source, copied[language], language == UILanguage.TraditionalChinese);
            Catalogs          = copied.ToFrozenDictionary();
            CompleteLanguages = copied.Where(pair => pair.Value.Count == source.Count && pair.Value.Values.All(static text => !string.IsNullOrWhiteSpace(text)))
                .Select(static pair => pair.Key).ToFrozenSet();
        }

        internal IReadOnlyDictionary<UILanguage, IReadOnlyDictionary<string, string>> Catalogs { get; }
        private FrozenSet<UILanguage> CompleteLanguages { get; }

        internal bool IsComplete(UILanguage value)
        {
            UILanguages.GetCode(value);
            return CompleteLanguages.Contains(value);
        }

        internal bool TryGet
        (
            string     key,
            UILanguage value,
            out string text
        )
        {
            UILanguages.GetCode(value);
            if (Catalogs[value].TryGetValue(key, out var translated) && !string.IsNullOrWhiteSpace(translated))
            {
                text = translated;
                return true;
            }
            text = string.Empty;
            return false;
        }
    }

    #region 常量

    private const string RESOURCE_PREFIX = "OmniToolbox.Localization.";

    #endregion
}
