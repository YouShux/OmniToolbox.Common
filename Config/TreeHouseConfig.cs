using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniToolbox.Config;

[Serializable]
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
        "FastWorldTravel"
    ];

    [NonSerialized]
    private readonly Dictionary<string, object> moduleConfigs = new(StringComparer.Ordinal);

    [NonSerialized]
    private readonly Dictionary<string, JToken> rawModuleConfigs = new(StringComparer.Ordinal);
    [NonSerialized]
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

    public void MigrateModuleName(string oldName, string newName)
    {
        if (ModuleStates.Remove(oldName, out var enabled) && !ModuleStates.ContainsKey(newName))
        {
            ModuleStates[newName] = enabled;
        }

        if (Favorites.Remove(oldName))
        {
            Favorites.Add(newName);
        }

        foreach (var modules in FavoriteGroups.Values)
        {
            if (modules.Remove(oldName))
            {
                modules.Add(newName);
            }
        }

        if (moduleConfigs.Remove(oldName, out var moduleConfig) && !moduleConfigs.ContainsKey(newName))
        {
            moduleConfigs[newName] = moduleConfig;
        }

        if (rawModuleConfigs.Remove(oldName, out var rawModuleConfig) &&
            !moduleConfigs.ContainsKey(newName) &&
            !rawModuleConfigs.ContainsKey(newName))
        {
            rawModuleConfigs[newName] = rawModuleConfig;
        }
    }

    public T GetOrCreate<T>(string moduleName) where T : class, new()
    {
        if (moduleConfigs.TryGetValue(moduleName, out var existing))
        {
            return existing as T
                ?? throw new InvalidOperationException(
                    $"TreeHouse module {moduleName} config is {existing.GetType().Name}, not {typeof(T).Name}.");
        }

        var config = rawModuleConfigs.TryGetValue(moduleName, out var raw)
            ? raw.ToObject<T>()
                ?? throw new InvalidOperationException($"TreeHouse module {moduleName} config contains no object.")
            : new T();
        rawModuleConfigs.Remove(moduleName);
        moduleConfigs[moduleName] = config;
        return config;
    }

    public object GetOrCreateModuleConfig(string moduleName, object defaultConfig)
    {
        ArgumentNullException.ThrowIfNull(defaultConfig);
        if (moduleConfigs.TryGetValue(moduleName, out var existing))
        {
            return existing;
        }

        var config = rawModuleConfigs.TryGetValue(moduleName, out var raw)
            ? raw.ToObject(defaultConfig.GetType()) ?? defaultConfig
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
            rawModuleConfigs[moduleName] = JToken.FromObject(config);
        }
        catch
        {
            moduleConfigs[moduleName] = config;
            throw;
        }
    }

    public bool ResetModuleSettings(string moduleName)
    {
        if (!moduleConfigs.TryGetValue(moduleName, out var config))
        {
            return false;
        }

        var type = config.GetType();
        var defaults = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unable to create default settings for {moduleName}.");
        foreach (var property in type.GetProperties())
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
