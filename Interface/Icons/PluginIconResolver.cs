using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using OmenTools.Dalamud.Helpers;
using OmniToolbox.Host;

namespace OmniToolbox.UI;

internal static class PluginIconResolver
{
    private const BindingFlags InstanceMemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly object? ImageCache =
        DalamudReflector.GetService("Dalamud.Interface.Internal.Windows.PluginImageCache");
    private static readonly FieldInfo? LocalPluginField = typeof(IExposedPlugin).Assembly
        .GetType("Dalamud.Plugin.ExposedPlugin")?
        .GetField("<plugin>P", InstanceMemberFlags);
    private static readonly MethodInfo? TryGetIconMethod = ImageCache?.GetType()
        .GetMethod("TryGetIcon", InstanceMemberFlags);
    private static readonly PropertyInfo? DefaultIconProperty = ImageCache?.GetType()
        .GetProperty("DefaultIcon", InstanceMemberFlags);
    private static readonly Dictionary<string, ISharedImmediateTexture?> LocalTextures =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string?> LocalIconPaths =
        new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsUsableTexture([NotNullWhen(true)] IDalamudTextureWrap? texture) =>
        texture is not null &&
        texture.Handle != nint.Zero &&
        Math.Max(texture.Width, texture.Height) > 4;

    internal static IDalamudTextureWrap? GetIcon(IExposedPlugin? plugin, float displaySize)
    {
        if (plugin is null)
        {
            return GetDefaultIcon();
        }

        if (GetLocalIconPath(plugin) is { } iconPath)
        {
            try
            {
                if (!LocalTextures.TryGetValue(iconPath, out var texture))
                {
                    texture = DalamudServices.TextureProvider.GetFromFile(iconPath);
                    LocalTextures[iconPath] = texture;
                }

                if (texture is not null &&
                    texture.TryGetWrap(out var localIcon, out _) &&
                    IsUsableTexture(localIcon))
                {
                    return localIcon;
                }
            }
            catch
            {
                LocalTextures[iconPath] = null;
            }
        }

        var localPlugin = GetLocalPlugin(plugin);
        if (localPlugin is not null && TryGetIconMethod is not null)
        {
            try
            {
                object?[] arguments = [localPlugin, plugin.Manifest, plugin.IsThirdParty, null, null];
                if (TryGetIconMethod.Invoke(ImageCache, arguments) is true &&
                    arguments[3] is IDalamudTextureWrap texture &&
                    IsUsableTexture(texture))
                {
                    return texture;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    internal static void Dispose()
    {
        LocalTextures.Clear();
        LocalIconPaths.Clear();
    }

    private static IDalamudTextureWrap? GetDefaultIcon()
    {
        return DefaultIconProperty?.GetValue(ImageCache) is IDalamudTextureWrap texture &&
               IsUsableTexture(texture)
            ? texture
            : null;
    }

    private static object? GetLocalPlugin(IExposedPlugin plugin)
    {
        try
        {
            if (LocalPluginField?.GetValue(plugin) is { } localPlugin)
            {
                return localPlugin;
            }
        }
        catch
        {
        }

        foreach (var field in plugin.GetType().GetFields(InstanceMemberFlags))
        {
            var typeName = field.FieldType.FullName ?? field.FieldType.Name;
            if (!typeName.Contains("LocalPlugin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (field.GetValue(plugin) is { } value)
                {
                    return value;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string? GetLocalIconPath(IExposedPlugin plugin)
    {
        var cacheKey = $"{plugin.InternalName}\0{plugin.Version}";
        if (LocalIconPaths.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        return LocalIconPaths[cacheKey] = FindLocalIconPath(plugin);
    }

    private static string? FindLocalIconPath(IExposedPlugin plugin)
    {
        var directories = new List<string>();
        if (GetLocalPlugin(plugin) is { } localPlugin)
        {
            var assembly = localPlugin.GetType().GetProperty("PluginAssembly", InstanceMemberFlags)?.GetValue(localPlugin) as Assembly ??
                           localPlugin.GetType().GetField("pluginAssembly", InstanceMemberFlags)?.GetValue(localPlugin) as Assembly;
            if (!string.IsNullOrWhiteSpace(assembly?.Location))
            {
                directories.Add(Path.GetDirectoryName(assembly.Location)!);
            }
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var launcher in new[] { "XIVLauncherCN", "XIVLauncher" })
        {
            var pluginRoot = Path.Combine(appData, launcher, "installedPlugins", plugin.InternalName);
            if (!Directory.Exists(pluginRoot))
            {
                continue;
            }

            var versionDirectory = Path.Combine(pluginRoot, plugin.Version.ToString());
            if (Directory.Exists(versionDirectory))
            {
                directories.Add(versionDirectory);
            }

            try
            {
                directories.AddRange(Directory.EnumerateDirectories(pluginRoot)
                    .OrderByDescending(Directory.GetLastWriteTimeUtc));
            }
            catch
            {
            }
        }

        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var relativePath in new[]
                     {
                         Path.Combine("images", "icon.png"),
                         Path.Combine("images", "icon.jpg"),
                         Path.Combine("images", "icon.jpeg"),
                         Path.Combine("images", "icon.webp"),
                         Path.Combine("images", "icon.bmp"),
                         "icon.png",
                         "icon.jpg",
                         "icon.jpeg",
                         "icon.webp",
                         "icon.bmp"
                     })
            {
                var path = Path.Combine(directory, relativePath);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }
}
