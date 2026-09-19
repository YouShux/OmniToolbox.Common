using Dalamud;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using OmenTools;
using OmenTools.OmenService;

namespace OmniToolbox.UI;

public static class OmniFonts
{
    internal static readonly KeyValuePair<string, string>[] GameFonts =
    [
        new("game:Axis", "Axis"),
        new("game:MiedingerMid", "Miedinger Medium"),
        new("game:Meidinger", "Miedinger"),
        new("game:TrumpGothic", "Trump Gothic")
    ];

    private static readonly Dictionary<float, IFontHandle> UIHandles = [];
    private static string CurrentPath = string.Empty;
    private static float CurrentSize;

    public static bool TryGetGameFamily(string path, out GameFontFamily family)
    {
        family = path switch
        {
            "game:Axis" => GameFontFamily.Axis,
            "game:MiedingerMid" => GameFontFamily.MiedingerMid,
            "game:Meidinger" => GameFontFamily.Meidinger,
            "game:TrumpGothic" => GameFontFamily.TrumpGothic,
            _ => GameFontFamily.Undefined
        };
        return family != GameFontFamily.Undefined;
    }

    public static IFontHandle GetUIFont(float scale = 1f)
    {
        var manager = FontManager.Instance();
        var path = manager.Config.FontFileName;
        var baseSize = manager.GetActualFontSize(1f);
        if (CurrentPath != path || CurrentSize != baseSize)
        {
            Dispose();
            CurrentPath = path;
            CurrentSize = baseSize;
        }
        if (!TryGetGameFamily(path, out var family))
        {
            return manager.GetUIFont(scale);
        }
        var size = manager.GetActualFontSize(scale);
        if (!UIHandles.TryGetValue(size, out var font))
        {
            font = CreateGameFont(family, size);
            UIHandles.Add(size, font);
        }
        return font.Available ? font : manager.GetUIFont(scale);
    }

    public static IFontHandle CreateGameFont(GameFontFamily family, float size) =>
        DService.Instance().UIBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = tk.AddGameGlyphs(new GameFontStyle(family, size), null, default);
            tk.AddGameSymbol(new() { SizePx = size, MergeFont = font });
            tk.AddFontAwesomeIconFont(new() { SizePx = size, MergeFont = font });
            // 游戏字体缺少的中文及其他字符由随 Dalamud 分发的字体补齐。
            tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular,
                new() { SizePx = size, MergeFont = font, FontNo = 2, GlyphRanges = [0x20, 0xFFEF, 0] });
            tk.Font = font;
        }));

    public static void Dispose()
    {
        foreach (var font in UIHandles.Values)
        {
            font.Dispose();
        }
        UIHandles.Clear();
    }
}
