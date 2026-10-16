using System.Globalization;
using System.Text.Json.Nodes;

namespace OmniToolbox.Operations;

public sealed record OperationSnapshot(
    string OperationID,
    string Service,
    string Kind,
    ulong CharacterContentID,
    OperationState State,
    string Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt)
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
