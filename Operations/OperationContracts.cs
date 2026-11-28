using System.Globalization;
using System.Text.Json.Nodes;

namespace OmniToolbox.Operations;

public interface IOperationService
{
    OperationSnapshot? LatestOperation { get; }

    OperationSnapshot? GetOperation(string operationID);

    bool CancelOperation(string operationID);

    event Action<OperationSnapshot>? OperationChanged;
}

public static class OperationResponse
{
    public static JsonObject Request
    (
        bool               accepted,
        OperationSnapshot? operation,
        string             rejectionError = "request_rejected"
    ) => new()
    {
        ["available"] = true,
        ["accepted"] = accepted,
        ["error"] = accepted ? null : rejectionError,
        ["operationID"] = accepted ? operation?.OperationID : null
    };

    public static JsonObject Get(OperationSnapshot? operation) => new()
    {
        ["available"] = true,
        ["found"] = operation is not null,
        ["error"] = operation is null ? "operation_not_found" : null,
        ["operation"] = operation?.ToJSON()
    };
}

public sealed record OperationSnapshot
(
    string          OperationID,
    string          Service,
    string          Kind,
    ulong           CharacterContentID,
    OperationState  State,
    string          Error,
    DateTimeOffset  StartedAt,
    DateTimeOffset? CompletedAt
)
{
    public bool ResultKnown => State is OperationState.Succeeded or OperationState.Failed or OperationState.Cancelled;

    public JsonObject ToJSON() => new()
    {
        ["operationID"] = OperationID,
        ["service"] = Service,
        ["kind"] = Kind,
        ["characterContentID"] = CharacterContentID.ToString(CultureInfo.InvariantCulture),
        ["state"] = State.ToString(),
        ["error"] = string.IsNullOrEmpty(Error) ? null : Error,
        ["startedAt"] = StartedAt.ToString("O", CultureInfo.InvariantCulture),
        ["completedAt"] = CompletedAt?.ToString("O", CultureInfo.InvariantCulture),
        ["resultKnown"] = ResultKnown
    };
}

public enum OperationState
{
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Unconfirmed
}
