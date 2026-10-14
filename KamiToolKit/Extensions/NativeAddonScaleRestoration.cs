using System.Collections;
using System.Reflection;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.BaseTypes;
using OmniToolbox.Host;

namespace OmniToolbox.UI;

internal sealed class NativeAddonScaleRestoration : IDisposable
{
    private readonly IDictionary addonConfigs;
    private readonly FieldInfo scaleField;
    private readonly IEnumerable<NativeAddon> nativeAddons;

    private NativeAddonScaleRestoration(IDictionary addonConfigs, FieldInfo scaleField, IEnumerable<NativeAddon> nativeAddons)
    {
        this.addonConfigs = addonConfigs;
        this.scaleField = scaleField;
        this.nativeAddons = nativeAddons;
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, OnPreSetup);
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnPostSetup);
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
            var addonsProperty = typeof(NativeAddon).GetProperty("CreatedAddons", BindingFlags.Static | BindingFlags.NonPublic)
                                 ?? throw new MissingMemberException(typeof(NativeAddon).FullName, "CreatedAddons");
            var addons = addonsProperty.GetValue(null) as IEnumerable<NativeAddon>
                         ?? throw new InvalidOperationException("KamiToolKit 原生窗口列表尚未初始化。");
            return new(configs, scale, addons);
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

    private unsafe void OnPostSetup(AddonEvent type, AddonArgs args)
    {
        if (!args.AddonName.StartsWith("Omni", StringComparison.Ordinal))
            return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        foreach (var window in nativeAddons)
        {
            if ((AtkUnitBase*)window != addon)
                continue;
            addon->EnableTitleBarContextMenu = window.EnableContextMenu;
            addon->DisableUserScaling = window.DisableScaleContextOption;
            return;
        }
    }

    public void Dispose()
    {
        DalamudServices.AddonLifecycle.UnregisterListener(OnPreSetup);
        DalamudServices.AddonLifecycle.UnregisterListener(OnPostSetup);
    }
}
