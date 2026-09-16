using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace OmniToolbox.Host;

public sealed class DalamudServices
{
    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    public static IChatGui ChatGUI { get; private set; } = null!;

    [PluginService]
    public static IFramework Framework { get; private set; } = null!;

    [PluginService]
    public static IKeyState KeyState { get; private set; } = null!;

    [PluginService]
    public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService]
    public static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    public static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    public static IPlayerState PlayerState { get; private set; } = null!;

    [PluginService]
    public static IUnlockState UnlockState { get; private set; } = null!;

    public static void Initialize(IDalamudPluginInterface pluginInterface) =>
        pluginInterface.Create<DalamudServices>();
}
