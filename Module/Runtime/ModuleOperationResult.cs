using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Runtime;

public readonly record struct ModuleOperationResult
(
    ModuleOperationStatus Status,
    string                Reason = ""
)
{
    public bool Succeeded => Status == ModuleOperationStatus.Succeeded;

    public static ModuleOperationResult Success => new(ModuleOperationStatus.Succeeded);

    public static ModuleOperationResult Failure(string reason) => new(ModuleOperationStatus.Failed, reason);

    public static ModuleOperationResult Cancelled => new(ModuleOperationStatus.Cancelled, OmniLoc.Get("Module.Operation.Cancelled"));

    public static ModuleOperationResult Busy => new(ModuleOperationStatus.Busy, OmniLoc.Get("Module.Operation.Busy"));
}
