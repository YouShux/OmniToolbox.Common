using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Commands;
using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Models;

public readonly record struct ModuleCommand(string DescriptionKey, string ClipboardText)
{
    public string DisplayCommand => OmniCommandCatalog.FormatDisplayText(ClipboardText);

    public string GetDisplayText(OmniModuleBase module) => $"{DisplayCommand} → {OmniLoc.Get(module, DescriptionKey)}";
}
