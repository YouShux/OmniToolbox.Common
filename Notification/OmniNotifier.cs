using System.IO;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures;
using Dalamud.Utility;
using Lumina.Text.ReadOnly;
using OmenTools;
using OmenTools.OmenService;
using OmniToolbox.Config;
using DalamudSeString = Dalamud.Game.Text.SeStringHandling.SeString;
using DalamudSeStringBuilder = Dalamud.Game.Text.SeStringHandling.SeStringBuilder;

namespace OmniToolbox.Notifications;

public static class OmniNotifier
{
    private static readonly Lazy<ISharedImmediateTexture?> PluginIcon      = new(LoadPluginIcon);
    private static WindowsPopupNotifier?                   WindowsNotifier;
    private static UIConfig?                               Config;

    private static readonly ReadOnlySeString ChatPrefix = new(
        new DalamudSeStringBuilder()
            .AddUiForeground(
                $"{(char)SeIconChar.BoxedLetterO}{(char)SeIconChar.BoxedLetterT}",
                10)
            .Build()
            .Encode());

    internal static void Configure(UIConfig? config) => Config = config;

    public static void Chat
    (
        string  title,
        string? details = null
    )
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var builder = new DalamudSeStringBuilder()
            .Append(title);
        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.Append("\n").Append(details);
        }

        Chat(builder.Build());
    }

    public static void Chat(DalamudSeString message)
        => Chat(new XivChatEntry { Message = message });

    public static void ChatError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Chat(new XivChatEntry
        {
            Type = XivChatType.ErrorMessage,
            Message = new DalamudSeStringBuilder().Append(message).Build()
        });
    }

    public static void Chat(XivChatEntry entry)
    {
        if (entry.Message.Payloads.Count == 0)
        {
            return;
        }

        using var rented = new RentedSeStringBuilder();
        var builder = rented.Builder;
        if (Config?.ShowOTPrefix != false)
            builder.Append(ChatPrefix).Append(" ");
        builder.Append(new ReadOnlySeString(entry.Message.Encode()));
        entry.Message = builder.ToReadOnlySeString().ToDalamudString();
        NotifyHelper.Chat(entry);
    }

    public static void Banner(string content) => Banner(content, 0);

    public static void Banner(string content, uint iconID)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        NotifyHelper.ToastQuest(content, new QuestToastOptions { IconId = iconID });
    }

    public static void Popup
    (
        string           title,
        string           content,
        NotificationType type = NotificationType.Info,
        bool             usePluginIcon = true,
        bool             notifyWhenBackground = true
    )
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

        DalamudServices.NotificationManager.AddNotification(
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
