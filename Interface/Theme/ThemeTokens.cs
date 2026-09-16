namespace OmniToolbox.UI.Theme;

public readonly record struct ThemeTokens(
    Vector4 Primary,
    Vector4 Secondary,
    Vector4 Accent,
    Vector4 Background,
    Vector4 Surface,
    Vector4 Text,
    Vector4 Success,
    Vector4 Warning,
    Vector4 Error,
    Vector4 Border,
    Vector4 Shadow,
    float ShadowOffset,
    float BorderRadius,
    float ButtonRadius,
    float BorderThickness,
    float HighlightStrength,
    Vector2 WindowPadding,
    Vector2 FramePadding,
    Vector2 ItemSpacing);
