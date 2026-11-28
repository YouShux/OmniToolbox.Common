using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OmenTools;
using OmenAetheryteList = OmenTools.Dalamud.Services.Game.UI.Abstractions.IAetheryteList;
using OmenObjectTable = OmenTools.Dalamud.Services.Game.Object.Abstractions.IObjectTable;

namespace OmniToolbox.Host;

public sealed class DalamudServices
{
    private static volatile bool isUnloading;

    public static bool IsUnloading => isUnloading;

    public static void BeginUnload() => isUnloading = true;

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
    public static IAgentLifecycle AgentLifecycle { get; private set; } = null!;

    [PluginService]
    public static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    public static ITextureSubstitutionProvider TextureSubstitution { get; private set; } = null!;

    [PluginService]
    public static ITextureReadbackProvider TextureReadback { get; private set; } = null!;

    [PluginService]
    public static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    public static IPlayerState PlayerState { get; private set; } = null!;

    [PluginService]
    public static IUnlockState UnlockState { get; private set; } = null!;

    [PluginService]
    public static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    public static ICondition Condition { get; private set; } = null!;

    [PluginService]
    public static IGameGui GameGUI { get; private set; } = null!;

    [PluginService]
    public static ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;

    [PluginService]
    public static IGameConfig GameConfig { get; private set; } = null!;

    [PluginService]
    public static IDutyState DutyState { get; private set; } = null!;

    [PluginService]
    public static IPartyList PartyList { get; private set; } = null!;

    [PluginService]
    public static IDtrBar DTRBar { get; private set; } = null!;

    [PluginService]
    public static IPartyFinderGui PartyFinder { get; private set; } = null!;

    [PluginService]
    public static IGameInventory GameInventory { get; private set; } = null!;

    [PluginService]
    public static IMarketBoard MarketBoard { get; private set; } = null!;

    [PluginService]
    public static ISigScanner SigScanner { get; private set; } = null!;

    [PluginService]
    public static IFlyTextGui FlyText { get; private set; } = null!;

    [PluginService]
    public static INotificationManager NotificationManager { get; private set; } = null!;

    [PluginService]
    public static IGamepadState Gamepad { get; private set; } = null!;

    /// <summary>
    /// 通过 PluginInterfaceAdapter.GetService 解析，保留 SafetyHook 兜底与 ALC 钩子追踪。
    /// </summary>
    public static IGameInteropProvider Hook { get; private set; } = null!;

    public static IUiBuilder UIBuilder => PluginInterface.UiBuilder;

    public static OmenObjectTable ObjectTable => DService.Instance().ObjectTable;

    public static OmenAetheryteList AetheryteList => DService.Instance().AetheryteList;

    public static Task RunOnFramework(Action action) => Framework.RunOnFrameworkThread(action);

    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        isUnloading = false;
        OmniToolbox.UI.OmniUIHost.Configure(() => IsUnloading, () =>
        {
            OmniToolbox.Game.CombatCharacterSnapshot.Clear();
            OmniToolbox.UI.IconBrowser.ClearDisplayCaches();
            OmniToolbox.UI.Controls.ItemSelectionTable.ClearDisplayCaches();
            OmniToolbox.UI.Controls.ItemRequirementLine.ClearDisplayCaches();
            OmniToolbox.UI.Controls.TerritorySelector.ClearDisplayCaches();
        });
        pluginInterface.Create<DalamudServices>();
        PluginInterface = pluginInterface;
        Hook = pluginInterface.GetService(typeof(IGameInteropProvider)) as IGameInteropProvider
            ?? throw new InvalidOperationException("Game interop service is unavailable.");
    }

    internal static object? GetInternalService(string fullName)
    {
        var serviceType = typeof(IDalamudPluginInterface).Assembly.GetType(fullName);
        return serviceType is null ? null : PluginInterface.GetService(serviceType);
    }
}
