using System.Numerics;

namespace OmniToolbox.Quests;

public enum QuestWorkState { Idle, Running, Succeeded, Failed }

public sealed record QuestTravelTarget(uint TerritoryID, Vector3 Position, float Radius = 3, bool AllowFlight = true);

public sealed record QuestWorkStatus(QuestWorkState State, string? Error = null);

/// <summary>仅控制 operationID 所有的任务移动，不接管已运行的其他导航。</summary>
public interface IQuestTravelService
{
    bool TryStart(string operationID, QuestTravelTarget target, out string? error);

    QuestWorkStatus Tick(string operationID);

    void Cancel(string operationID);
}
