using OmenTools.OmenService;
using OmniToolbox.Collections;
using OmniToolbox.Config;

namespace OmniToolbox.UI.Theme;

public static class OmniTheme
{
    internal static readonly ThemeTokens LineGreenTokens = new(
        new Vector4(0.663f, 0.702f, 0.533f, 0.80f),
        new Vector4(0.560f, 0.620f, 0.470f, 0.86f),
        new Vector4(0.996f, 0.980f, 0.878f, 0.50f),
        new Vector4(0.996f, 0.980f, 0.878f, 1f),
        new Vector4(0.663f, 0.702f, 0.533f, 0.40f),
        new Vector4(0.120f, 0.160f, 0.120f, 0.94f),
        new Vector4(0.373f, 0.435f, 0.322f, 1f),
        Orange,
        new Vector4(0.665f, 0.323f, 0.323f, 0.96f),
        new Vector4(1f, 1f, 1f, 1f),
        new Vector4(0f, 0f, 0f, 0.04f),
        4f,
        10f,
        10f,
        1.85f,
        0.05f,
        new Vector2(8f),
        new Vector2(8f, 8f),
        new Vector2(8f, 6f));

    private static readonly ThemeTokens LineMidnightIrisTokens = LineGreenTokens with
    {
        Primary = new Vector4(0.125f, 0.141f, 0.204f, 1f),
        Secondary = new Vector4(0.345f, 0.396f, 0.490f, 1f),
        Accent = new Vector4(0.192f, 0.220f, 0.310f, 1f),
        Background = new Vector4(0.071f, 0.078f, 0.118f, 1f),
        Surface = new Vector4(0.114f, 0.129f, 0.192f, 1f),
        Text = new Vector4(0.914f, 0.906f, 0.945f, 1f),
        Success = new Vector4(0.373f, 0.435f, 0.322f, 1f),
        Warning = new Vector4(0.812f, 0.725f, 0.510f, 1f),
        Error = new Vector4(0.827f, 0.604f, 0.682f, 1f),
        Border = new Vector4(0.337f, 0.369f, 0.467f, 1f),
        Shadow = new Vector4(0f, 0f, 0f, 0.24f),
        HighlightStrength = 0.06f
    };

    private static readonly ThemeTokens LinePeachBloomTokens = LineGreenTokens with
    {
        Primary = new Vector4(0.918f, 0.722f, 0.741f, 0.80f),
        Secondary = new Vector4(0.851f, 0.549f, 0.588f, 0.86f),
        Accent = new Vector4(0.988f, 0.937f, 0.945f, 0.50f),
        Background = new Vector4(1f, 0.976f, 0.976f, 1f),
        Surface = new Vector4(0.918f, 0.722f, 0.741f, 0.40f),
        Text = new Vector4(0.314f, 0.188f, 0.208f, 0.94f),
        Success = new Vector4(0.373f, 0.435f, 0.322f, 1f),
        Error = new Vector4(0.647f, 0.341f, 0.396f, 0.96f)
    };

    private static readonly ThemeTokens LineGraphiteTokens = LineGreenTokens with
    {
        Primary = new Vector4(0.145f, 0.161f, 0.180f, 1f),
        Secondary = new Vector4(0.349f, 0.380f, 0.424f, 1f),
        Accent = new Vector4(0.188f, 0.208f, 0.231f, 1f),
        Background = new Vector4(0.071f, 0.078f, 0.086f, 1f),
        Surface = new Vector4(0.114f, 0.125f, 0.141f, 1f),
        Text = new Vector4(0.925f, 0.937f, 0.949f, 1f),
        Success = new Vector4(0.373f, 0.435f, 0.322f, 1f),
        Warning = new Vector4(0.780f, 0.690f, 0.471f, 1f),
        Error = new Vector4(0.788f, 0.498f, 0.529f, 1f),
        Border = new Vector4(0.357f, 0.388f, 0.431f, 1f),
        Shadow = new Vector4(0f, 0f, 0f, 0.24f),
        HighlightStrength = 0.06f
    };

    private static readonly ThemeTokens OfficeGlowTokens = LineGraphiteTokens with
    {
        Primary = new Vector4(48f / 255f, 52f / 255f, 58f / 255f, 1f),
        Secondary = new Vector4(76f / 255f, 82f / 255f, 94f / 255f, 1f),
        Accent = new Vector4(64f / 255f, 73f / 255f, 92f / 255f, 1f),
        Background = new Vector4(35f / 255f, 38f / 255f, 42f / 255f, 1f),
        Surface = new Vector4(58f / 255f, 63f / 255f, 70f / 255f, 1f),
        Text = new Vector4(241f / 255f, 245f / 255f, 249f / 255f, 1f),
        Border = new Vector4(1f, 1f, 1f, 0.15f),
        Shadow = new Vector4(0f, 0f, 0f, 0.24f),
        ShadowOffset = 2f,
        BorderRadius = 10f,
        ButtonRadius = 8f,
        BorderThickness = 1f,
        HighlightStrength = 0.08f
    };

    public const float DefaultFontSize = 16f;
    public const float MinimumFontSize = 4f;
    public const float REFERENCE_FONT_SIZE = 18f;
    public const float MaximumFontSize = 48f;
    public const ushort OrangeColorType = 500;
    public const ushort ShopColorType = 43;
    public static string DefaultFontPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
        "msyh.ttc");

    public static float DalamudScaleValue => Math.Clamp(ImGuiHelpers.GlobalScale, 0.5f, 3f);

    public static float ScaleValue => scopedScale ??
        Math.Clamp(FontManager.Instance().Config.FontSize, MinimumFontSize, MaximumFontSize) /
        REFERENCE_FONT_SIZE *
        DalamudScaleValue;

    public static UITheme CurrentTheme { get; set; } = UITheme.LineGreen;

    internal static ThemeTokens? LiquidGlassTokens { get; set; }

    internal delegate bool GlassSurfacePainter(
        ImDrawListPtr drawList, Vector2 pos, Vector2 size, Vector4 fill, float radius,
        float opacity, bool sampleBackground, ImDrawFlags corners, uint interactionID, bool selected);

    internal static GlassSurfacePainter? GlassPainter { get; set; }

    [ThreadStatic] private static ThemeTokens? scopedTokens;
    [ThreadStatic] private static Vector4? scopedControlAccent;
    [ThreadStatic] private static float? scopedScale;

    public static ThemeTokens Tokens => scopedTokens ?? BaseTokens;

    public static ThemeTokens BaseTokens => CurrentTheme switch
    {
        UITheme.LinePeachBloom => LinePeachBloomTokens,
        UITheme.LineGraphite => LineGraphiteTokens,
        UITheme.LineMidnightIris => LineMidnightIrisTokens,
        UITheme.LiquidGlass => LiquidGlassTokens ?? LineGraphiteTokens,
        UITheme.OfficeGlow => OfficeGlowTokens,
        _ => LineGreenTokens
    };

    public static bool IsGlass => CurrentTheme == UITheme.LiquidGlass;

    public static bool UsesMaterial => IsGlass || CurrentTheme == UITheme.OfficeGlow;

    internal static bool HasCustomBackground => scopedTokens is { } tokens && tokens.Background != BaseTokens.Background;

    internal static bool HasCustomBorder => scopedTokens is { } tokens && tokens.Border != BaseTokens.Border;

    public static bool UsesDarkPalette => CurrentTheme is
        UITheme.OfficeGlow or
        UITheme.LiquidGlass or
        UITheme.LineMidnightIris or
        UITheme.LineGraphite;

    public static Vector4 HoverBackground => IsGlass
        ? Vector4.Lerp(Tokens.Surface, Tokens.Accent, 0.30f)
        : UsesDarkPalette
            ? Vector4.Lerp(Tokens.Surface, Tokens.Secondary, 0.75f) with { W = 1f }
            : Tokens.Primary with { W = Math.Clamp(Tokens.HighlightStrength * 5f, 0f, 1f) };

    public static Vector4 ActiveBackground => IsGlass
        ? Vector4.Lerp(Tokens.Surface, Tokens.Accent, 0.60f)
        : Tokens.Secondary with { W = 1f };

    public static Vector4 ControlAccent => scopedControlAccent ?? (CurrentTheme switch
    {
        UITheme.LineMidnightIris => new Vector4(0.616f, 0.655f, 0.737f, 1f),
        UITheme.LinePeachBloom => new Vector4(0.784f, 0.451f, 0.494f, 1f),
        UITheme.LineGraphite => new Vector4(0.608f, 0.643f, 0.686f, 1f),
        UITheme.LiquidGlass => Tokens.Accent with { W = 1f },
        UITheme.OfficeGlow => new Vector4(125f / 255f, 211f / 255f, 252f / 255f, 1f),
        _ => Tokens.Success
    });

    internal static void ResetMaterialState()
    {
        GlassMotion.Clear();
        MaterialPainter.Clear();
    }

    internal static Vector4 TooltipBackground => scopedTokens?.Background ?? Tokens.Primary;

    // 仅覆盖当前绘制调用链，退出后恢复，避免工具栏配色影响其他窗口。
    public readonly struct ColorScope : IDisposable
    {
        private readonly ThemeTokens? previousTokens;
        private readonly Vector4? previousAccent;

        public ColorScope(ThemeTokens tokens, Vector4? controlAccent = null)
        {
            previousTokens = scopedTokens;
            previousAccent = scopedControlAccent;
            scopedTokens = tokens;
            scopedControlAccent = controlAccent;
        }

        public void Dispose()
        {
            scopedTokens = previousTokens;
            scopedControlAccent = previousAccent;
        }
    }

    public static float Scale(float value) => value * ScaleValue;

    // 局部窗口按当前字号布局，退出后恢复外层尺寸，支持嵌套弹窗。
    public readonly struct ScaleScope : IDisposable
    {
        private readonly float? previousScale;

        public ScaleScope(float scale)
        {
            previousScale = scopedScale;
            scopedScale = scale;
        }

        public void Dispose() => scopedScale = previousScale;
    }

    public static Vector2 Scale(Vector2 value) => value * ScaleValue;

    public static Vector2 MainWindowSize(Vector2 configuredSize) =>
        Scale(configuredSize == Vector2.Zero ? UIConfig.DefaultMainWindowSize : configuredSize);

    public static Vector2 MainWindowMinSize() => Scale(new Vector2(320f, 240f));

    public static Vector2 CollapsedWindowSize(float width) => ClampWindowSize(new(MathF.Max(Scale(420f), width), TitleBarHeight() + Scale(4f)));

    public static Vector2 Unscale(Vector2 value) => value / ScaleValue;

    public static Vector2 ClampWindowSize(Vector2 size) =>
        Vector2.Clamp(size, Vector2.One, Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize));

    public static Vector2 ClampWindowPosition(Vector2 position, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();
        return Vector2.Clamp(position, viewport.WorkPos,
            Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - ClampWindowSize(size)));
    }

    public static Vector2 PreserveWindowSize(Vector2 actual, Vector2 expected)
    {
        var viewport = ImGui.GetMainViewport().WorkSize;
        return new(actual.X >= viewport.X - 1f && expected.X > viewport.X ? expected.X : actual.X,
            actual.Y >= viewport.Y - 1f && expected.Y > viewport.Y ? expected.Y : actual.Y);
    }

    public static float ChromeFrameInset() => Scale(4f);

    public static float CollapsedHeaderSafeInset() => Scale(8f);

    public static float CollapsedHeaderTop() => Scale(1.6f);

    public static float TitleBarHeight() => MathF.Ceiling(ImGui.GetTextLineHeight() + Scale(8f));

    public static float WindowInset() => ContentGap();

    public static Vector2 PopupPadding() => new(ChromeFrameInset() + WindowInset());

    public static Vector2 TitleIconSize() => new(ImGui.GetTextLineHeight());

    public static float TitleIconTop() => (TitleBarHeight() - TitleIconSize().Y) * 0.5f;

    public static float TitleExpandIconRight() => TitleCloseIconRight() + TitleIconSize().X + Scale(5f);

    public static float TitleCloseIconRight() => TitleIconSize().X + Scale(5f);

    public static Vector2 SidebarWidth() => new(Scale(164f), 0f);

    public static Vector2 SidebarIconSize() => Scale(new Vector2(60f, 60f));

    public static Vector2 StatusIconSize(float height) => new(MathF.Round(height * (24f / 32f)), height);

    public static Vector2 FitImageSize(Vector2 sourceSize, float maximumDimension) =>
        sourceSize * (maximumDimension / MathF.Max(sourceSize.X, sourceSize.Y));

    public static Vector2 NavButtonSize() => new(Scale(148f), Scale(34f));

    public static float SidebarHeaderGap() => Scale(8f);

    public static float SidebarFooterHeight() => ImGui.GetTextLineHeight() * 2f + ImGui.GetStyle().ItemSpacing.Y;

    public static Vector2 SmallButtonSize() => new(Scale(116f), Scale(34f));

    public static float TableItemIconSize() => Scale(40f);

    public static Vector2 FeatureCardSize() => new(0f, Scale(78f));

    public static float ContentGap() => Scale(Tokens.WindowPadding.X);

    public static float SectionHeaderHeight() => Scale(34f);

    public static float BorderThickness() => Scale(Tokens.BorderThickness);

    public static float MainPanelBorderThickness() => Scale(2.4f);

    public static float CheckboxSize() => Scale(34f);

    public static float CheckboxRounding() => Scale(9f);

    public static float CheckboxBorderThickness(bool selected) => Scale(selected ? 2.5f : 1.9f);

    public static Vector4 AvailabilityPurchasable => new(0.16f, 0.68f, 0.28f, 1f);

    public static Vector4 AvailabilityObtainable => Orange;


    public static Vector4 ItemRarityGreen => new(0f, 0.8f, 0.13333334f, 1f);

    public static Vector4 Orange => new(0.82f, 0.40f, 0.08f, 1f);

    public static Vector4 AvailabilityColor(ItemAvailability availability) => availability switch
    {
        ItemAvailability.Obtainable => AvailabilityObtainable,
        ItemAvailability.Purchasable => AvailabilityPurchasable,
        ItemAvailability.Unobtainable => Tokens.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null)
    };

    public static Vector4 Favorite => new(1f, 0.71f, 0.76f, 1f);

    public static uint Color(Vector4 color) =>
        ImGui.ColorConvertFloat4ToU32(color with { W = color.W * ImGui.GetStyle().Alpha });
}
