
using OmniToolbox.UI;

namespace OmniToolbox.Tooltips;

internal static class DesynthesisEnhancementState
{
    public static DesynthesisLevelInfo Calculate
    (
        float currentLevel,
        uint  recommendedLevel
    )
    {
        var current = (int)MathF.Floor(currentLevel);
        var recommended = (int)recommendedLevel;
        return new(
            current,
            recommended,
            current >= recommended + 50
                ? DesynthesisStatus.Green
                : current >= recommended
                    ? DesynthesisStatus.Yellow
                    : DesynthesisStatus.Red);
    }

    public static ushort GetColorKey(DesynthesisStatus status) => status switch
    {
        DesynthesisStatus.Green => 43,
        DesynthesisStatus.Yellow => 506,
        DesynthesisStatus.Red => 14,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string FormatTooltipValue
    (
        DesynthesisLevelInfo level,
        string               jobName
    ) => string.Format(
        OmniLoc.Get(string.IsNullOrWhiteSpace(jobName)
            ? "Feature.DesynthesisEnhancement.TooltipValueWithoutJob"
            : "Feature.DesynthesisEnhancement.TooltipValue"),
        level.Recommended,
        level.Current,
        jobName);
}
