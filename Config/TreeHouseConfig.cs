using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniToolbox.Config;

public sealed class TreeHouseConfig
{
    private static readonly string[] DefaultEnabledModuleNames =
    [
        "ItemPriceQuery",
        "ItemQuery",
        "ItemPreview",
        "VoidPurchase",
        "VoidNPCShop",
        "VoidGlamourStation",
        "VoidFreeCompanyChest",
        "FastWorldTravel",
        "BetterTeleportManagement",
        "AutoTalkSkip",
        "AutoCutsceneSkip",
        "FastSearchResults"
    ];

    private readonly Dictionary<string, object> moduleConfigs = new(StringComparer.Ordinal);

    private readonly Dictionary<string, JToken> rawModuleConfigs = new(StringComparer.Ordinal);
    private readonly HashSet<string> localModuleNames = new(StringComparer.Ordinal);

    public Dictionary<string, bool> ModuleStates { get; set; } = new(StringComparer.Ordinal)
    {
        ["ItemPriceQuery"] = true,
        ["ItemQuery"] = true,
        ["ItemPreview"] = true,
        ["VoidPurchase"] = true,
        ["VoidNPCShop"] = true,
        ["ItemManagement"] = false,
        ["FastWorldTravel"] = true
    };

    public HashSet<string> Favorites { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, HashSet<string>> FavoriteGroups { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public IEnumerable<string> ConfiguredModuleNames => moduleConfigs.Keys.Concat(rawModuleConfigs.Keys);

    public bool IsEnabled(string moduleName) =>
        ModuleStates.TryGetValue(moduleName, out var enabled) && enabled;

    public void MarkLocalModule(string moduleName) => localModuleNames.Add(moduleName);

    public bool IsLocalModule(string moduleName) => localModuleNames.Contains(moduleName);

    public void SetEnabled(string moduleName, bool enabled) => ModuleStates[moduleName] = enabled;

    public void EnsureDefaultModuleStates()
    {
        foreach (var moduleName in DefaultEnabledModuleNames)
        {
            ModuleStates.TryAdd(moduleName, true);
        }
    }

    public T GetOrCreate<T>(string moduleName) where T : class, new()
    {
        CollectibleConfigJSON.Validate(typeof(T));
        if (moduleConfigs.TryGetValue(moduleName, out var existing))
        {
            return existing as T
                ?? throw new InvalidOperationException(
                    $"TreeHouse module {moduleName} config is {existing.GetType().Name}, not {typeof(T).Name}.");
        }

        var config = rawModuleConfigs.TryGetValue(moduleName, out var raw)
            ? (T?)CollectibleConfigJSON.Deserialize(raw, typeof(T))
                ?? throw new InvalidOperationException($"TreeHouse module {moduleName} config contains no object.")
            : new T();
        ValidateModuleConfig(config);
        rawModuleConfigs.Remove(moduleName);
        moduleConfigs[moduleName] = config;
        return config;
    }

    public object GetOrCreateModuleConfig(string moduleName, object defaultConfig)
    {
        ArgumentNullException.ThrowIfNull(defaultConfig);
        ValidateModuleConfig(defaultConfig);
        if (moduleConfigs.TryGetValue(moduleName, out var existing))
        {
            return existing;
        }

        var config = rawModuleConfigs.TryGetValue(moduleName, out var raw)
            ? CollectibleConfigJSON.Deserialize(raw, defaultConfig.GetType()) ?? defaultConfig
            : defaultConfig;
        rawModuleConfigs.Remove(moduleName);
        moduleConfigs[moduleName] = config;
        return config;
    }

    public void MoveModuleConfigToRaw(string moduleName)
    {
        if (!moduleConfigs.Remove(moduleName, out var config))
        {
            return;
        }

        try
        {
            rawModuleConfigs[moduleName] = CollectibleConfigJSON.Serialize(config);
        }
        catch
        {
            moduleConfigs[moduleName] = config;
            throw;
        }
    }

    internal ModuleConfigSnapshot CaptureModuleConfig(string moduleName) => new(
        moduleConfigs.GetValueOrDefault(moduleName),
        rawModuleConfigs.GetValueOrDefault(moduleName)?.DeepClone(),
        localModuleNames.Contains(moduleName),
        ModuleStates.TryGetValue(moduleName, out var enabled) ? enabled : null);

    internal static void ValidateModuleConfig(object config) => CollectibleConfigJSON.Validate(config);

    internal JToken SerializeModuleConfig(string moduleName, JsonSerializerSettings? settings = null) =>
        CollectibleConfigJSON.Serialize(moduleConfigs[moduleName], settings);

    internal void RestoreModuleConfig(string moduleName, ModuleConfigSnapshot snapshot)
    {
        moduleConfigs.Remove(moduleName);
        rawModuleConfigs.Remove(moduleName);
        if (snapshot.Materialized is not null) moduleConfigs[moduleName] = snapshot.Materialized;
        if (snapshot.Raw is not null) rawModuleConfigs[moduleName] = snapshot.Raw.DeepClone();
        if (snapshot.IsLocal) localModuleNames.Add(moduleName);
        else localModuleNames.Remove(moduleName);
        if (snapshot.Enabled is { } enabled) ModuleStates[moduleName] = enabled;
        else ModuleStates.Remove(moduleName);
    }

    internal sealed record ModuleConfigSnapshot(object? Materialized, JToken? Raw, bool IsLocal, bool? Enabled);

    public bool ResetModuleSettings(string moduleName)
    {
        if (!moduleConfigs.TryGetValue(moduleName, out var config))
        {
            return false;
        }

        var type = config.GetType();
        var defaults = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unable to create default settings for {moduleName}.");
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!field.IsInitOnly && !field.IsLiteral)
            {
                field.SetValue(config, field.GetValue(defaults));
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.CanRead &&
                property.CanWrite &&
                property.GetMethod is { IsStatic: false } &&
                property.GetIndexParameters().Length == 0)
            {
                property.SetValue(config, property.GetValue(defaults));
            }
        }

        return true;
    }

    public void SetRawModuleConfig(string moduleName, JToken config)
    {
        if (!moduleConfigs.ContainsKey(moduleName))
        {
            rawModuleConfigs[moduleName] = config.DeepClone();
        }
    }

    public bool TryGetModuleConfig(string moduleName, out object? config) =>
        moduleConfigs.TryGetValue(moduleName, out config);

    public bool TryGetRawModuleConfig(string moduleName, out JToken? config) =>
        rawModuleConfigs.TryGetValue(moduleName, out config);
}
