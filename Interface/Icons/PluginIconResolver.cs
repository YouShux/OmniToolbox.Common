using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Microsoft.VisualBasic.FileIO;
using OmenTools;
using OmniToolbox.Host;
using TreeTools.Lifecycle;

namespace OmniToolbox.UI;

internal static class PluginIconResolver
{
    private static readonly object? ImageCache =
        DalamudServices.GetInternalService("Dalamud.Interface.Internal.Windows.PluginImageCache");
    private static readonly FieldInfo? LocalPluginField = typeof(IExposedPlugin).Assembly
        .GetType("Dalamud.Plugin.ExposedPlugin")?
        .GetField("<plugin>P", INSTANCE_MEMBER_FLAGS);
    private static readonly MethodInfo? TryGetIconMethod = ImageCache?.GetType()
        .GetMethod("TryGetIcon", INSTANCE_MEMBER_FLAGS);
    private static readonly PropertyInfo? DefaultIconProperty = ImageCache?.GetType()
        .GetProperty("DefaultIcon", INSTANCE_MEMBER_FLAGS);
    private static readonly Dictionary<string, ISharedImmediateTexture?> LocalTextures =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string?> LocalIconPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CachedIcon> CachedIcons = new(StringComparer.Ordinal);
    private static readonly string CacheDirectory = Path.Combine(
        DalamudServices.PluginInterface.GetPluginConfigDirectory(), "TreeHouse", "MultiToolbar", "Cache", "PluginIcons");
    private static readonly FeatureLifetime CacheLifetime = new();
    private static Guid? PNGContainerGUID;
    private static bool CacheMigrated;

    static PluginIconResolver()
    {
        CacheLifetime.Add(CachedIcons.Clear);
        CacheLifetime.Add(LocalTextures.Clear);
        CacheLifetime.Add(LocalIconPaths.Clear);
    }

    internal static bool IsUsableTexture([NotNullWhen(true)] IDalamudTextureWrap? texture) =>
        texture is not null &&
        texture.Handle != nint.Zero &&
        Math.Max(texture.Width, texture.Height) > 4;

    internal static IDalamudTextureWrap? GetIcon(IExposedPlugin? plugin, float displaySize)
    {
        if (CacheLifetime.IsStopping)
            return null;

        if (plugin is null)
        {
            return GetDefaultIcon();
        }

        MigrateCache();
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

        var cacheKey = $"{plugin.InternalName}\0{plugin.Version}\0{plugin.Manifest.IconUrl}";
        if (!CachedIcons.TryGetValue(cacheKey, out var cachedIcon))
        {
            var prefix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plugin.InternalName))) + "-";
            var fileName = prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey))) + ".png";
            cachedIcon = new(Path.Combine(CacheDirectory, fileName), prefix);
            CachedIcons.Add(cacheKey, cachedIcon);
        }

        if (cachedIcon.SaveTask is { IsCompleted: true } saveTask)
        {
            cachedIcon.SaveTask = null;
            if (saveTask.IsCompletedSuccessfully && saveTask.Result)
            {
                DService.Instance().TextureSubstitution.InvalidatePaths([cachedIcon.Path]);
                cachedIcon.Texture = null;
                cachedIcon.FileChecked = false;
            }
        }
        try
        {
            if (!cachedIcon.FileChecked)
            {
                cachedIcon.FileChecked = true;
                if (File.Exists(cachedIcon.Path))
                    cachedIcon.Texture = DalamudServices.TextureProvider.GetFromFile(cachedIcon.Path);
            }
            if (cachedIcon.Texture is { } cachedTexture &&
                cachedTexture.TryGetWrap(out var diskIcon, out _) && IsUsableTexture(diskIcon))
            {
                if (!cachedIcon.OldCacheRecycled)
                {
                    RecycleOldCache(cachedIcon.Path, cachedIcon.Prefix);
                    cachedIcon.OldCacheRecycled = true;
                }
                return diskIcon;
            }
        }
        catch
        {
            cachedIcon.Texture = null;
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
                    if (cachedIcon.SaveTask is null && Environment.TickCount64 >= cachedIcon.RetryAfter)
                    {
                        cachedIcon.RetryAfter = Environment.TickCount64 + 60_000;
                        cachedIcon.SaveTask = SaveIconAsync(texture, cachedIcon.Path, CacheLifetime.CancellationToken);
                        CacheLifetime.Track(cachedIcon.SaveTask);
                    }
                    return texture;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    internal static ValueTask DisposeAsync() => CacheLifetime.DisposeAsync();

    private static async Task<bool> SaveIconAsync(IDalamudTextureWrap texture, string path, CancellationToken cancellationToken)
    {
        try
        {
            var readback = DService.Instance().TextureReadback;
            PNGContainerGUID ??= readback.GetSupportedImageEncoderInfos()
                .FirstOrDefault(codec => codec.MimeTypes.Contains("image/png", StringComparer.OrdinalIgnoreCase))?.ContainerGuid;
            if (PNGContainerGUID is not { } containerGUID)
                return false;

            Directory.CreateDirectory(CacheDirectory);
            await readback.SaveToFileAsync(texture.CreateWrapSharingLowLevelResource(), containerGUID, path,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            DalamudServices.PluginLog.Warning(exception, $"Unable to save plugin icon cache: {path}");
            return false;
        }
    }

    private static void MigrateCache()
    {
        if (CacheMigrated)
            return;
        CacheMigrated = true;
        try
        {
            if (!Directory.Exists(CacheDirectory))
                return;
            foreach (var plugin in DalamudServices.PluginInterface.InstalledPlugins
                         .GroupBy(plugin => plugin.InternalName, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.OrderByDescending(plugin => plugin.IsLoaded)
                             .ThenByDescending(plugin => plugin.Version).First()))
            {
                var key = $"{plugin.InternalName}\0{plugin.Version}\0{plugin.Manifest.IconUrl}";
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
                var oldPath = Path.Combine(CacheDirectory, hash + ".png");
                if (!File.Exists(oldPath))
                    continue;
                var prefix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plugin.InternalName))) + "-";
                var newPath = Path.Combine(CacheDirectory, prefix + hash + ".png");
                if (!File.Exists(newPath))
                    File.Move(oldPath, newPath);
            }
            RecycleOldCache(null, string.Empty);
        }
        catch (Exception)
        {
        }
    }

    private static void RecycleOldCache(string? currentPath, string prefix)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(CacheDirectory, prefix + "*.png"))
            {
                if (path.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                var name = Path.GetFileNameWithoutExtension(path);
                var hash = name[prefix.Length..];
                if (hash.Length != 64 || !hash.All(char.IsAsciiHexDigit))
                    continue;
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
        }
        catch (Exception)
        {
        }
    }

    private static IDalamudTextureWrap? GetDefaultIcon() => DefaultIconProperty?.GetValue(ImageCache) is IDalamudTextureWrap texture &&
               IsUsableTexture(texture)
            ? texture
            : null;

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

        foreach (var field in plugin.GetType().GetFields(INSTANCE_MEMBER_FLAGS))
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
            var assembly = localPlugin.GetType().GetProperty("PluginAssembly", INSTANCE_MEMBER_FLAGS)?.GetValue(localPlugin) as Assembly ??
                           localPlugin.GetType().GetField("pluginAssembly", INSTANCE_MEMBER_FLAGS)?.GetValue(localPlugin) as Assembly;
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

    private sealed class CachedIcon(string path, string prefix)
    {
        public string Path { get; } = path;
        public string Prefix { get; } = prefix;
        public bool FileChecked;
        public bool OldCacheRecycled;
        public ISharedImmediateTexture? Texture;
        public Task<bool>? SaveTask;
        public long RetryAfter;
    }

    #region 常量

    private const BindingFlags INSTANCE_MEMBER_FLAGS =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    #endregion
}
