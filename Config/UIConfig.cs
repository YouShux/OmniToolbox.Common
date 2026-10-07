namespace OmniToolbox.Config;

[Serializable]
public sealed class UIConfig
{
    public static readonly Vector2 DefaultMainWindowSize = new(1420f, 900f);
    public static readonly Vector2 DefaultItemInformationWindowSize = new(860f, 500f);

    public Vector2 MainWindowSize { get; set; } = DefaultMainWindowSize;

    public Vector2 ItemInformationWindowSize { get; set; } = DefaultItemInformationWindowSize;

    public int ActiveTab { get; set; } = 3;

    public HashSet<string> ExpandedSettingsSections { get; set; } = [];

    public string LastReadChangelogVersion { get; set; } = string.Empty;

    public bool ShowLauncherIcon { get; set; } = true;

    public float LauncherIconSize { get; set; } = DefaultLauncherIconSize;

    public float LauncherIconOpacity { get; set; } = DefaultLauncherIconOpacity;

    public Vector2 LauncherIconPosition { get; set; } = new(26f, 260f);

    public UILanguage Language { get; set; } = UILanguage.SimplifiedChinese;

    public UITheme Theme { get; set; } = UITheme.LineGreen;

    public bool LiquidGlassEnabled = true;

    public bool GlassBlurEnabled = true;

    public bool GlassBorderEnabled = true;

    public float GlassMaskOpacity = DEFAULT_GLASS_MASK_OPACITY;

    public float GlassBlurStrength = DEFAULT_GLASS_BLUR_STRENGTH;

    public NumberDisplayMode NumberDisplayMode { get; set; } = NumberDisplayMode.Standard;

    #region 常量

    public const float DefaultLauncherIconSize = 60f;

    public const float DefaultLauncherIconOpacity = 1f;

    public const float DEFAULT_GLASS_MASK_OPACITY = 0.20f;

    public const float DEFAULT_GLASS_BLUR_STRENGTH = 1.00f;

    #endregion
}

public enum UILanguage
{
    SimplifiedChinese,
    TraditionalChinese
}

public enum UITheme
{
    LineGreen = 0,
    LinePeachBloom = 5,
    LineMidnightIris = 2,
    LineGraphite = 8,
    OfficeGlow = 11,
    LiquidGlass = 12
}

public enum UIMotionMode
{
    Full,
    Reduced,
    Off
}

public enum NumberDisplayMode
{
    Standard,
    Chinese
}
