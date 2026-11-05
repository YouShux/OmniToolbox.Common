namespace OmniToolbox.Quests;

public enum QuestTargetType { Quest, SatisfactionSupply, AlliedSociety, UnlockLink }
public enum QuestRequestMode { Targets, Catalog }
public enum QuestTargetRelation { All, Any }
public enum QuestAvailability { Satisfied, Ready, Waiting, Blocked, Unsupported }
public enum QuestExecutionPhase { Idle, Starting, Planning, Travelling, Interacting, Fighting, Duty, Crafting, Gathering, Fishing, Waiting, Paused, Stopping, Finished }

public sealed record QuestTarget(QuestTargetType Type, uint ID, byte? Sequence = null)
{
    public QuestTarget Normalize() => Type == QuestTargetType.Quest && ID is > 0 and < 0x10000
        ? this with { ID = ID | 0x10000 } : this;
}

public sealed record QuestRequest
{
    public string Caller { get; init; } = string.Empty;
    public QuestRequestMode Mode { get; init; }
    public QuestTarget[] Targets { get; init; } = [];
    public QuestTargetRelation Relation { get; init; }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Caller) && Caller.Length <= 128 &&
        Enum.IsDefined(Mode) && Enum.IsDefined(Relation) && Targets is { Length: <= 8192 } &&
        (Mode == QuestRequestMode.Catalog ? Relation == QuestTargetRelation.All : Targets.Length > 0) &&
        Targets.All(static target => target is not null && target.ID != 0 && Enum.IsDefined(target.Type) &&
            (target.Type != QuestTargetType.Quest || target.ID < 0x20000) &&
            (target.Type == QuestTargetType.Quest || target.Sequence is null));
}

public sealed record QuestBlocker(string Code, uint? RelatedID = null, DateTimeOffset? RetryAt = null);

public sealed record QuestTargetEvaluation
(
    QuestTarget Target,
    string Name,
    QuestAvailability Availability,
    QuestBlocker[] Blockers,
    QuestTarget[] Prerequisites,
    string Coverage = "Unverified"
);

public sealed record QuestEvaluation(bool Satisfied, bool CanExecute, QuestTargetEvaluation[] Targets, string? Error = null);

public sealed record QuestCatalogQuery(int Offset = 0, int Limit = 100, string Search = "", QuestTargetType? Type = null);

public sealed record QuestCatalogEntry(QuestTarget Target, string Name, QuestAvailability Availability, string Coverage, QuestBlocker[] Blockers);

public sealed record QuestCatalogPage(int Total, int Offset, QuestCatalogEntry[] Entries, string BaselineHash, string ResourceVersion);

public sealed record QuestStartResult(bool Accepted, string? OperationID, string? Error = null);

public sealed record QuestTargetResult(QuestTarget Target, QuestAvailability Availability, QuestBlocker[] Blockers);

public sealed record QuestOperationResult
(
    Operations.OperationSnapshot Operation,
    string Caller,
    QuestTargetRelation Relation,
    QuestTargetResult[] Targets
);

public sealed record QuestServiceStatus
(
    string? OperationID,
    QuestExecutionPhase Phase,
    QuestTarget? CurrentTarget,
    byte Sequence,
    string? Step,
    string? Reason,
    int Completed,
    int Remaining,
    int Blocked
);
