using System.Threading;
using System.Threading.Tasks;
using OmniToolbox.Operations;

namespace OmniToolbox.Quests;

/// <summary>任务服务的读写入口；游戏状态查询及控制在 Framework 线程执行。</summary>
public interface IQuestService : IOperationService
{
    QuestCatalogPage GetCatalog(QuestCatalogQuery query);

    QuestEvaluation Evaluate(QuestRequest request);

    Task<QuestStartResult> StartAsync(QuestRequest request, CancellationToken cancellationToken = default);

    QuestServiceStatus GetStatus();

    QuestOperationResult? GetQuestOperation(string operationID);

    bool Pause(string operationID);

    bool Resume(string operationID);
}
