namespace OmniToolbox.TreeHouse.InterfaceOperation;

public interface IFreeCompanyActionService
{
    bool TryPurchaseAction
    (
        int        actionID,
        out string failureReasonKey
    );
}
