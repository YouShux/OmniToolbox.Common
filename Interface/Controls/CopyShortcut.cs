using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using OmniToolbox.Host;

namespace OmniToolbox.UI;

public static class CopyShortcut
{
    public static bool IsPressed(ref bool held)
    {
        var keys = DalamudServices.KeyState;
        if (!keys[VirtualKey.CONTROL] || !keys[VirtualKey.C] || keys[VirtualKey.MENU] || keys[VirtualKey.SHIFT])
        {
            held = false;
            return false;
        }
        if (held)
            return false;
        held = true;
        return true;
    }

    public static unsafe void Consume()
    {
        // 保留 Ctrl 和其他按键，仅阻止复制用的 C 再触发游戏绑定。
        DalamudServices.KeyState[VirtualKey.C] = false;
        var input = UIInputData.Instance();
        if (input is not null)
            input->KeyboardInputs.KeyState[(int)SeVirtualKey.C] = default;
        var framework = Framework.Instance();
        if (framework is not null)
        {
            framework->KeyboardInputs.KeyState[(int)SeVirtualKey.C]  = default;
            framework->KeyboardInputs2.KeyState[(int)SeVirtualKey.C] = default;
        }
    }
}
