namespace OmniToolbox.Common.Module.Abstractions;

public interface IModuleAutomation
{
    IReadOnlyList<string> AutomationActions { get; }

    bool IsAutomationBusy { get; }

    string AutomationState { get; }

    bool TryRunAutomation(string action, string arguments, out string error);

    bool StopAutomation();
}
