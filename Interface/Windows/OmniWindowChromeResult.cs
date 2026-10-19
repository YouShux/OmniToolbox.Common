using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI;

public readonly record struct OmniWindowChromeResult(
    bool CollapseClicked,
    bool CloseClicked,
    bool TitleDoubleClicked)
{
    public bool ToggleCollapse => CollapseClicked || TitleDoubleClicked;
}
