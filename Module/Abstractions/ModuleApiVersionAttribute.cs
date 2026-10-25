namespace OmniToolbox.Common.Module.Abstractions;

/// <summary>
/// 声明具体模块实现的 Omni 模块接口版本。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModuleApiVersionAttribute(int version) : Attribute
{
    /// <summary>
    /// 获取模块实现的接口版本。
    /// </summary>
    public int Version { get; } = version;
}
