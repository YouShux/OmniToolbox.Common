namespace OmniToolbox.Config;

[Serializable]
public sealed class UIConfig
{
    public static readonly Vector2 DefaultMainWindowSize = new(1420f, 900f);
    public static readonly Vector2 DefaultItemInformationWindowSize = new(860f, 500f);
    public const float DefaultLauncherIconSize = 60f;
    public const float DefaultLauncherIconOpacity = 1f;

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

    public GlassQuality GlassQuality { get; set; } = GlassQuality.Standard;

    public UIMotionMode MotionMode { get; set; } = UIMotionMode.Full;

    public NumberDisplayMode NumberDisplayMode { get; set; } = NumberDisplayMode.Standard;
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
    GlassLight = 9,
    GlassDark = 10,
    OfficeGlow = 11
}

public enum GlassQuality
{
    Standard,
    Light,
    Compatible
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
