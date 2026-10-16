using System.Text.Json.Nodes;

namespace OmniToolbox.Operations;

public static class OperationResponse
{
    public static JsonObject Request(bool accepted, OperationSnapshot? operation, string rejectionError = "request_rejected") => new()
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
