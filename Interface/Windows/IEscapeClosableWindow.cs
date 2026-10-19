using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using OmniToolbox.Host;

namespace OmniToolbox.UI;

public interface IEscapeClosableWindow
{
    bool IsOpen { get; }

    bool IsFocused { get; }

    void Close();
}
