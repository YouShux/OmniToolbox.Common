using OmniToolbox.Commands;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Models;

public sealed record PluginCollectionOption
(
    Guid   ID,
    string Name
);

public readonly record struct ModuleCommand
(
    string DescriptionKey,
    string ClipboardText
)
{
    public string DisplayCommand => OmniCommandCatalog.FormatDisplayText(ClipboardText);

    public string GetDisplayText(OmniModuleBase module)
    {
        return $"{OmniCommandCatalog.FormatModuleDisplayText(ClipboardText, module)} → " +
               OmniLoc.Get(module, DescriptionKey);
    }
}

public sealed class ModuleInfo
{
    public required ModuleCategory Category { get; init; }

    public bool RequiresPrivateProvider { get; init; }

    public bool RequiresElevatedAccess { get; init; }

    public int RequiredAccessLevel { get; init; } = 1;

    public bool ShowsAutoPathStatus { get; init; }
    public bool IsUnderDevelopment { get; init; }

    public IReadOnlyList<ModuleCommand>? Commands { get; init; }

    public string Author { get; init; } = "YouShu";

    public IReadOnlyList<string>? SupportUrls { get; init; }

    public string? ReportURL { get; init; }

    public string? PreviewImageURL { get; init; }

    public IReadOnlyList<string>? PreviewImageURLs { get; init; }
}

public sealed record ToolbarCommandOption
(
    string Name,
    string Command
);
