using OmenTools.Extensions;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static class OmniNumberFormatter
{
    private static NumberDisplayMode Mode;

    public static void SetMode(NumberDisplayMode value) => Mode = value;

    public static string Format<T>(T value) where T : IBinaryInteger<T>, IFormattable =>
        Mode == NumberDisplayMode.Chinese ? value.ToChineseString() : value.ToString("N0", null);
}
