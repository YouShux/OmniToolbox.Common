namespace OmniToolbox.Operations;

public sealed class OperationTracker(string service)
{
    private readonly Dictionary<string, OperationSnapshot> operations = [];
    private readonly Queue<string>                         order      = [];
    private readonly object                                sync       = new();
    private OperationSnapshot?                             latest;

    public OperationSnapshot? Latest
    {
        get
        {
            lock (sync)
                return latest;
        }
    }

    public OperationSnapshot? Current => Latest is { State: OperationState.Running } current ? current : null;

    public event Action<OperationSnapshot>? Changed;

    public OperationSnapshot Begin
    (
        string kind,
        ulong  characterContentID
    )
    {
        var snapshot = new OperationSnapshot(Guid.NewGuid().ToString("N"), service, kind, characterContentID,
            OperationState.Running, string.Empty, DateTimeOffset.UtcNow, null);
        lock (sync)
        {
            if (latest is { State: OperationState.Running })
                throw new InvalidOperationException("An operation is already running.");

            operations.Add(snapshot.OperationID, snapshot);
            order.Enqueue(snapshot.OperationID);
            while (order.Count > MAX_HISTORY)
                operations.Remove(order.Dequeue());

            latest = snapshot;
        }
        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public void Complete
    (
        OperationState state,
        string         error = ""
    )
    {
        if (state == OperationState.Running)
            throw new ArgumentOutOfRangeException(nameof(state));
        OperationSnapshot snapshot;
        lock (sync)
        {
            if (latest is not { State: OperationState.Running } current)
                return;

            snapshot                         = current with
            {
                State = state,
                Error = error,
                CompletedAt = DateTimeOffset.UtcNow
            };
            operations[snapshot.OperationID] = snapshot;
            latest                           = snapshot;
        }
        Changed?.Invoke(snapshot);
    }

    public OperationSnapshot? Get(string operationID)
    {
        lock (sync)
            return operationID is { Length: 32 } && operations.TryGetValue(operationID, out var snapshot) ? snapshot : null;
    }

    public bool IsCurrent
    (
        string operationID,
        ulong  characterContentID
    ) =>
        characterContentID != 0 && Current is { } current && current.OperationID == operationID &&
        current.CharacterContentID == characterContentID;

    #region 常量

    private const int MAX_HISTORY = 128;

    #endregion
}
