using OmniToolbox.Common.Module.Enums;

namespace OmniToolbox.Common.Module.Models;

public sealed class ModuleInfo
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required ModuleCategory Category { get; init; }

    public bool RequiresPrivateProvider { get; init; }

    public bool RequiresElevatedAccess { get; init; }

    public int RequiredAccessLevel { get; init; } = 1;

    public bool ShowsAutoPathStatus { get; init; }

    public IReadOnlyList<ModuleCommand>? Commands { get; init; }

    public string Author { get; init; } = "YouShu";

    public IReadOnlyList<string>? SupportUrls { get; init; }

    public string? ReportURL { get; init; }

    public string? PreviewImageURL { get; init; }

    public IReadOnlyList<string>? PreviewImageURLs { get; init; }
}
