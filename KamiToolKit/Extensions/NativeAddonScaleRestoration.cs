using System.Collections;
using System.Reflection;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using OmniToolbox.Host;

namespace OmniToolbox.UI;

internal sealed class NativeAddonScaleRestoration : IDisposable
{
    private readonly IDictionary addonConfigs;
    private readonly FieldInfo scaleField;

    private NativeAddonScaleRestoration(IDictionary addonConfigs, FieldInfo scaleField)
    {
        this.addonConfigs = addonConfigs;
        this.scaleField = scaleField;
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, OnPreSetup);
    }

    public static NativeAddonScaleRestoration? Create()
    {
        try
        {
            // 当前 Kami 未公开配置读取接口；复用其内存配置，保持原有保存与迁移链路。
            var configFileField = typeof(KamiToolKitLibrary).GetField("AddonConfigFile", BindingFlags.Static | BindingFlags.NonPublic)
                                  ?? throw new MissingFieldException(typeof(KamiToolKitLibrary).FullName, "AddonConfigFile");
            var configFile = configFileField.GetValue(null)
                             ?? throw new InvalidOperationException("KamiToolKit 窗口配置尚未初始化。");
            var configsField = configFile.GetType().GetField("AddonConfigs")
                               ?? throw new MissingFieldException(configFile.GetType().FullName, "AddonConfigs");
            var configs = (IDictionary)configsField.GetValue(configFile)!;
            var configType = configsField.FieldType.GetGenericArguments()[1];
            var scale = configType.GetField("Scale")
                        ?? throw new MissingFieldException(configType.FullName, "Scale");
            return new(configs, scale);
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Error(ex, "初始化原生窗口缩放恢复失败。");
            return null;
        }
    }

    private unsafe void OnPreSetup(AddonEvent type, AddonArgs args)
    {
        if (!args.AddonName.StartsWith("Omni", StringComparison.Ordinal) ||
            addonConfigs[args.AddonName] is not { } config)
            return;

        try
        {
            // Kami 的初始状态跳过 100%；先补回该值，随后窗口自身的贴附缩放仍然优先。
            if (scaleField.GetValue(config) is 1f)
                ((AtkUnitBase*)args.Addon.Address)->SetScale(1f, true);
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Error(ex, "恢复原生窗口 {AddonName} 缩放失败。", args.AddonName);
            Dispose();
        }
    }

    public void Dispose() => DalamudServices.AddonLifecycle.UnregisterListener(OnPreSetup);
}
