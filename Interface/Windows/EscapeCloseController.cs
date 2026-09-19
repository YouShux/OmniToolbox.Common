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

public sealed class EscapeCloseController
{
    private const int ESCAPE_VIRTUAL_KEY = 0x1B;

    private bool escapePressedLastFrame;
    private bool suppressEscapeUntilRelease;

    public void Update(
        IEscapeClosableWindow first,
        IEscapeClosableWindow? second = null,
        IEscapeClosableWindow? third = null)
    {
        var escapePressed = (GetAsyncKeyState(ESCAPE_VIRTUAL_KEY) & 0x8000) != 0;
        if (!escapePressed)
        {
            escapePressedLastFrame = false;
            suppressEscapeUntilRelease = false;
            return;
        }

        if (suppressEscapeUntilRelease)
        {
            DalamudServices.KeyState[VirtualKey.ESCAPE] = false;
            return;
        }

        if (escapePressedLastFrame)
        {
            return;
        }

        escapePressedLastFrame = true;
        var focusedWindow = ResolveFocusedWindow(first, second, third);
        if (focusedWindow is null)
        {
            return;
        }

        focusedWindow.Close();
        suppressEscapeUntilRelease = true;
        DalamudServices.KeyState[VirtualKey.ESCAPE] = false;
    }

    private static IEscapeClosableWindow? ResolveFocusedWindow(
        IEscapeClosableWindow first,
        IEscapeClosableWindow? second,
        IEscapeClosableWindow? third)
    {
        if (first.IsOpen && first.IsFocused)
        {
            return first;
        }

        if (second is { IsOpen: true, IsFocused: true })
        {
            return second;
        }

        return third is { IsOpen: true, IsFocused: true } ? third : null;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
