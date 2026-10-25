using TreeTools.Text;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using OmniToolbox.Collections;
using OmniToolbox.Commands;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static class OmniLoc
{
    private static readonly object Sync = new();
    private static readonly ConcurrentDictionary<string, ModuleRegistration> Modules = new(StringComparer.Ordinal);
    private static FrozenDictionary<string, TextSource> Texts = new Dictionary<string, TextSource>().ToFrozenDictionary(StringComparer.Ordinal);
    private static long ResourceRevision;
    private static int Language;

    static OmniLoc()
    {
        var texts = new Dictionary<string, TextSource>(StringComparer.Ordinal);
        foreach (var source in ReadResources(typeof(OmniLoc).Assembly))
            foreach (var key in source.Simplified.Keys)
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
    public static void SetLanguage(UILanguage value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, null);
        if (Interlocked.Exchange(ref Language, (int)value) != (int)value)
            NotifyChanged();
    }

    public static string Get(string key) => Get(key, CurrentLanguage);

    public static string Get(string key, UILanguage value) => TryGet(key, value, out var text)
        ? text : throw new KeyNotFoundException($"Official localization key is not registered: {key}");

    /// <summary>
    /// 查询模块自带词条。模块之间的同名键相互隔离，缺失词条会明确报错。
    /// </summary>
    public static string Get(OmniModuleBase owner, string key) => Get(owner, key, CurrentLanguage);

    /// <summary>
    /// 按指定语言查询模块自带词条，缺失词条会明确报错。
    /// </summary>
    public static string Get(OmniModuleBase owner, string key, UILanguage value) =>
        TryGet(owner, key, value, out var text) ? text
            : throw new KeyNotFoundException($"Module localization key is not registered: {owner.ModuleName}/{key}");

    /// <summary>
    /// 尝试按当前语言获取模块自带词条。
    /// </summary>
    public static bool TryGet(OmniModuleBase owner, string key, out string text) =>
        TryGet(owner, key, CurrentLanguage, out text);

    /// <summary>
    /// 尝试按指定语言获取模块自带词条。
    /// </summary>
    public static bool TryGet(OmniModuleBase owner, string key, UILanguage value, out string text)
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
    public static bool TryGet(string key, UILanguage value, out string text)
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
                if (!texts.SimplifiedChinese.ContainsKey(command.DescriptionKey))
                    throw new InvalidDataException($"Module command localization is missing: {owner.ModuleName}/{command.DescriptionKey}");
        var registration = new ModuleRegistration(owner.ModuleName, new TextSource(texts.SimplifiedChinese));
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

    internal static string GetOfficialSource(string key) => Volatile.Read(ref Texts).TryGetValue(key, out var source)
        ? source.Simplified[key] : throw new KeyNotFoundException($"Official localization key is not registered: {key}");

    private static IReadOnlyList<TextSource> ReadResources(Assembly assembly) =>
        ReadResources(assembly.GetManifestResourceNames(), name => assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Missing localization resource: {name}"));

    internal static IReadOnlyList<TextSource> ReadResources(IReadOnlyCollection<string> resourceNames, Func<string, Stream> openResource)
    {
        var names = resourceNames.Where(static name => name.StartsWith(RESOURCE_PREFIX, StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        List<TextSource> result = [];
        foreach (var name in names.Order(StringComparer.Ordinal))
        {
            if (name.EndsWith(TRADITIONAL_SUFFIX, StringComparison.Ordinal))
            {
                if (!names.Contains(name[..^TRADITIONAL_SUFFIX.Length] + SIMPLIFIED_SUFFIX))
                    throw new InvalidDataException($"Traditional localization resource has no source: {name}");
                continue;
            }
            if (!name.EndsWith(SIMPLIFIED_SUFFIX, StringComparison.Ordinal))
                throw new InvalidDataException($"Unexpected localization resource: {name}");
            var traditionalName = name[..^SIMPLIFIED_SUFFIX.Length] + TRADITIONAL_SUFFIX;
            if (!names.Contains(traditionalName))
                throw new InvalidDataException($"Traditional localization resource is required: {traditionalName}");
            using var simplified = openResource(name);
            using var traditional = openResource(traditionalName);
            result.Add(ReadSource(simplified, traditional));
        }
        return result;
    }

    internal static TextSource ReadSource(Stream simplifiedStream, Stream traditionalStream)
    {
        var simplified = XliffResourceParser.Parse(simplifiedStream);
        var traditional = XliffResourceParser.Parse(traditionalStream, true);
        XliffResourceParser.ValidatePair(simplified, traditional);
        return new(simplified, traditional);
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
        private readonly string moduleName;
        private ModuleRegistration? previous;
        private volatile bool activated;
        private volatile bool disposed;

        internal ModuleRegistration(string moduleName, TextSource source)
        {
            this.moduleName = moduleName;
            Source = source;
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
                previous = Modules.GetValueOrDefault(moduleName);
                Modules[moduleName] = this;
                activated = true;
                changed = Source.Simplified.Count > 0 || previous?.Source.Simplified.Count is > 0;
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
                    changed = Source.Simplified.Count > 0 || previous?.Source.Simplified.Count is > 0;
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
        private readonly FrozenDictionary<string, string>? traditional;
        private readonly ConcurrentDictionary<string, string> converted = new(StringComparer.Ordinal);

        internal TextSource(IReadOnlyDictionary<string, string> simplified)
        {
            Simplified = XliffResourceParser.CopyTexts(simplified);
        }

        internal TextSource(IReadOnlyDictionary<string, string> simplified, IReadOnlyDictionary<string, string> traditional)
            : this(simplified)
        {
            this.traditional = XliffResourceParser.CopyTexts(traditional);
            XliffResourceParser.ValidatePair(Simplified, this.traditional);
        }

        internal FrozenDictionary<string, string> Simplified { get; }

        internal bool TryGet(string key, UILanguage value, out string text)
        {
            if (!Simplified.TryGetValue(key, out var source))
            {
                text = string.Empty;
                return false;
            }
            text = value switch
            {
                UILanguage.SimplifiedChinese => source,
                UILanguage.TraditionalChinese => traditional is not null ? traditional[key]
                    : converted.GetOrAdd(key, static (_, original) => ChineseTextConverter.ToTraditional(original), source),
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
            return true;
        }
    }

    #region 常量

    private const string RESOURCE_PREFIX = "OmniToolbox.Localization.";
    private const string SIMPLIFIED_SUFFIX = ".ChineseSimplified.xlf";
    private const string TRADITIONAL_SUFFIX = ".TraditionalChinese.xlf";

    #endregion
}
