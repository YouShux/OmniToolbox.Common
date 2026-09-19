using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace OmniToolbox.Notifications;

internal sealed class WindowsPopupNotifier : IDisposable
{
    private const uint ICON_ID = 0x4F4D4E49;

    private readonly Icon icon;
    private readonly nint windowHandle;
    private bool disposed;

    public WindowsPopupNotifier(string iconPath)
    {
        windowHandle = Process.GetCurrentProcess().MainWindowHandle;
        if (windowHandle == 0)
        {
            throw new InvalidOperationException("无法获取 Omni Toolbox Windows 通知图标所需的游戏窗口句柄。");
        }

        using var bitmap = new Bitmap(iconPath);
        var handle = bitmap.GetHicon();
        try
        {
            icon = (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }

        var data = CreateData();
        data.Flags = NotifyIconFlags.Icon | NotifyIconFlags.ToolTip | NotifyIconFlags.State;
        data.IconHandle = icon.Handle;
        data.ToolTip = "Omni Toolbox";
        data.State = NotifyIconState.Hidden;
        data.StateMask = NotifyIconState.Hidden;

        if (!ShellNotifyIcon(NotifyIconMessage.Add, ref data))
        {
            var error = Marshal.GetLastPInvokeError();
            icon.Dispose();
            throw new InvalidOperationException(
                $"无法初始化 Omni Toolbox Windows 通知图标。Win32Error={error}, WindowHandle=0x{windowHandle:X}。");
        }

        data.Flags = NotifyIconFlags.None;
        data.VersionOrTimeout = 4;
        _ = ShellNotifyIcon(NotifyIconMessage.SetVersion, ref data);
    }

    public void Show(string title, string content)
    {
        if (disposed)
        {
            return;
        }

        var data = CreateData();
        data.Flags = NotifyIconFlags.Info;
        data.Info = Truncate(content, 255);
        data.InfoTitle = Truncate(title, 63);
        data.InfoFlags = NotifyInfoFlags.User | NotifyInfoFlags.LargeIcon;
        data.BalloonIconHandle = icon.Handle;
        _ = ShellNotifyIcon(NotifyIconMessage.Modify, ref data);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        var data = CreateData();
        data.Flags = NotifyIconFlags.None;
        _ = ShellNotifyIcon(NotifyIconMessage.Delete, ref data);
        icon.Dispose();
    }

    private NotifyIconData CreateData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = windowHandle,
        ID = ICON_ID,
        ToolTip = string.Empty,
        Info = string.Empty,
        InfoTitle = string.Empty
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    [DllImport(
        "shell32.dll",
        EntryPoint = "Shell_NotifyIconW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(NotifyIconMessage message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint WindowHandle;
        public uint ID;
        public NotifyIconFlags Flags;
        public uint CallbackMessage;
        public nint IconHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ToolTip;

        public NotifyIconState State;
        public NotifyIconState StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint VersionOrTimeout;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public NotifyInfoFlags InfoFlags;
        public Guid ItemGUID;
        public nint BalloonIconHandle;
    }

    private enum NotifyIconMessage : uint
    {
        Add,
        Modify,
        Delete,
        SetFocus,
        SetVersion
    }

    [Flags]
    private enum NotifyIconFlags : uint
    {
        None = 0,
        Icon = 0x02,
        ToolTip = 0x04,
        State = 0x08,
        Info = 0x10
    }

    [Flags]
    private enum NotifyIconState : uint
    {
        Hidden = 0x01
    }

    [Flags]
    private enum NotifyInfoFlags : uint
    {
        User = 0x04,
        LargeIcon = 0x20
    }
}
