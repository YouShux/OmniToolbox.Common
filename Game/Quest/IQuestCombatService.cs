namespace OmniToolbox.Quests;

/// <summary>任务战斗会话；仅停止由指定请求取得的输出及移动控制。</summary>
public interface IQuestCombatService
{
    bool TryStart(string operationID, out string? error);

    QuestWorkStatus Tick(string operationID);

    void Cancel(string operationID);
}
