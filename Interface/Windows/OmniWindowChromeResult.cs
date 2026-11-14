namespace OmniToolbox.UI;

public readonly record struct OmniWindowChromeResult
(
    bool CollapseClicked,
    bool CloseClicked,
    bool TitleDoubleClicked
)
{
    public bool ToggleCollapse => CollapseClicked || TitleDoubleClicked;

    public bool ApplyCollapse(ref bool collapsed)
    {
        if (!ToggleCollapse) return false;
        collapsed = !collapsed;
        return true;
    }
}
