using System.IO;
using System.Linq;
using System.Reflection;
using OmniToolbox.Common.Module.Abstractions;

namespace OmniToolbox.Common.Module.Runtime;

internal static class ModuleContract
{
    internal const int API_VERSION = 1;

    internal static string UpgradeReason(string name, string detail) =>
        $"模块「{name}」使用旧版或无效写法，已拒绝加载。请作者升级并迁移至 OmniModuleBase、新生命周期入口和必填 Localization，重新编译并更新清单。原因：{detail}";

    internal static void ValidateVersion(string name, int version)
    {
        if (version > API_VERSION)
            throw new InvalidDataException($"模块「{name}」需要接口版本 {version}，请升级 Omni。");
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
