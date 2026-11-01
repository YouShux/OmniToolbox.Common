using OmenTools.Extensions;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static class OmniNumberFormatter
{
    public static NumberDisplayMode Mode { get; private set; }

    public static void SetMode(NumberDisplayMode value) => Mode = value;

    public static string Format<T>(T value) where T : IBinaryInteger<T>, IFormattable =>
        Mode == NumberDisplayMode.Chinese ? value.ToChineseString() : value.ToString("N0", null);

    public static string SecondsFormat(string numberFormat) =>
        Mode == NumberDisplayMode.Chinese ? $"{numberFormat}秒" : $"{numberFormat} s";

    public static string MillisecondsFormat(string numberFormat) =>
        Mode == NumberDisplayMode.Chinese ? $"{numberFormat}毫秒" : $"{numberFormat} ms";

    public static string FormatSeconds<T>(T value, string? format = null) where T : IFormattable =>
        SecondsFormat(value.ToString(format, null));

    public static string FormatMilliseconds<T>(T value, string? format = null) where T : IFormattable =>
        MillisecondsFormat(value.ToString(format, null));
}
