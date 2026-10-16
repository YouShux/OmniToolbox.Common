using OmniToolbox.Commands;
using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Models;

public readonly record struct ModuleCommand(string DescriptionKey, string ClipboardText)
{
    public string DisplayCommand => OmniCommandCatalog.FormatDisplayText(ClipboardText);

    public string DisplayText => $"{DisplayCommand} → {OmniLoc.Get(DescriptionKey)}";
}
