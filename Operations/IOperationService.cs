namespace OmniToolbox.Operations;

public interface IOperationService
{
    OperationSnapshot? LatestOperation { get; }

    OperationSnapshot? GetOperation(string operationID);

    bool CancelOperation(string operationID);

    event Action<OperationSnapshot>? OperationChanged;
}
