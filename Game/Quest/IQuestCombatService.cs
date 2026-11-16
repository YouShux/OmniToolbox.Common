namespace OmniToolbox.Quests;

/// <summary>任务战斗会话；仅停止由指定请求取得的输出及移动控制。</summary>
public interface IQuestCombatService
{
    bool TryStart(string operationID, out string? error);

    bool TryStart(string operationID, ulong targetID, out string? error) => TryStart(operationID, out error);

    bool TryUpdateTarget(string operationID, ulong targetID) => true;

    QuestWorkStatus Tick(string operationID);

    void Cancel(string operationID);
}
