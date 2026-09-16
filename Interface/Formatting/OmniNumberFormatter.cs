using System.Numerics;
using OmenTools.Extensions;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static class OmniNumberFormatter
{
    private static NumberDisplayMode mode;

    public static void SetMode(NumberDisplayMode value) => mode = value;

    public static string Format<T>(T value) where T : IBinaryInteger<T>, IFormattable =>
        mode == NumberDisplayMode.Chinese ? value.ToChineseString() : value.ToString("N0", null);
}
