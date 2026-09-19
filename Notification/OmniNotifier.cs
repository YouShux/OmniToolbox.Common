using System.IO;
using Dalamud.Game.Text;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures;
using Lumina.Text.ReadOnly;
using OmenTools;
using OmenTools.OmenService;
using OmniToolbox.Host;
using DalamudSeString = Dalamud.Game.Text.SeStringHandling.SeString;
using DalamudSeStringBuilder = Dalamud.Game.Text.SeStringHandling.SeStringBuilder;

namespace OmniToolbox.Notifications;

public static class OmniNotifier
{
    private static readonly Lazy<ISharedImmediateTexture?> PluginIcon = new(LoadPluginIcon);
    private static WindowsPopupNotifier? WindowsNotifier;

    private static readonly ReadOnlySeString ChatPrefix = new(
        new DalamudSeStringBuilder()
            .AddUiForeground(
                $"{(char)SeIconChar.BoxedLetterO}{(char)SeIconChar.BoxedLetterT}",
                10)
            .Build()
            .Encode());

    public static void Chat(string title, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var builder = new DalamudSeStringBuilder()
            .AddUiForeground(1)
            .Append(title);
        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.Append("\n").Append(details);
        }

        builder.AddUiForegroundOff();
        Chat(builder.Build());
    }

    public static void Chat(DalamudSeString message)
    {
        if (message.Payloads.Count == 0)
        {
            return;
        }

        NotifyHelper.Instance().Chat(new ReadOnlySeString(message.Encode()), ChatPrefix);
    }

    public static void Banner(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        NotifyHelper.ToastQuest(content);
    }

    public static void Popup(
        string title,
        string content,
        NotificationType type = NotificationType.Info,
        bool usePluginIcon = true,
        bool notifyWhenBackground = true)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? content : title;
        if (notifyWhenBackground && !GameState.IsForeground)
        {
            WindowsNotifier?.Show(resolvedTitle, content);
        }

        DService.Instance().DalamudNotification.AddNotification(
            new()
            {
                Title = resolvedTitle,
                Content = content,
                Type = type,
                InitialDuration = TimeSpan.FromSeconds(6),
                Minimized = false,
                IconTexture = usePluginIcon ? PluginIcon.Value : null
            });
    }

    public static void InitializeWindowsNotifications(string iconPath)
    {
        WindowsNotifier?.Dispose();
        WindowsNotifier = new(iconPath);
    }

    public static void DisposeWindowsNotifications()
    {
        WindowsNotifier?.Dispose();
        WindowsNotifier = null;
    }

    private static ISharedImmediateTexture? LoadPluginIcon()
    {
        var path = Path.Combine(
            DalamudServices.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
            "images",
            "icon.png");
        return File.Exists(path) ? DalamudServices.TextureProvider.GetFromFile(path) : null;
    }
}
