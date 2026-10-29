using System.IO;
using System.Reflection;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Runtime;

internal static class ModuleContract
{
    internal const int API_VERSION = 1;

    internal static string UpgradeReason
    (
        string name,
        string detail
    ) =>
        string.Format(OmniLoc.Get("Module.Contract.UpgradeReason"), name, detail);

    internal static void ValidateVersion
    (
        string name,
        int    version
    )
    {
        if (version > API_VERSION)
            throw new InvalidDataException(string.Format(OmniLoc.Get("Module.Contract.NewerApi"), name, version));
        if (version != API_VERSION)
            throw new InvalidDataException(UpgradeReason(name, "缺少或使用旧的模块接口 ModuleApiVersion 声明"));
    }

    internal static void ValidateType(Type type)
    {
        ValidateVersion(type.Name, type.GetCustomAttribute<ModuleApiVersionAttribute>(false)?.Version ?? 0);
        if (type.IsAbstract || !typeof(OmniModuleBase).IsAssignableFrom(type))
            throw new InvalidDataException(UpgradeReason(type.Name, "模块基类不正确"));
        for (var current = type; current != typeof(OmniModuleBase) && current is not null; current = current.BaseType)
        {
            if (current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Any(static method => method.Name is "OnEnable" or "OnDisable" or "OnDispose"))
                throw new InvalidDataException(UpgradeReason(type.Name, "仍声明旧生命周期入口"));
        }
    }
}
