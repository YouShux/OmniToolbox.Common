using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Config;
using OmniToolbox.Items;
using OmniToolbox.Operations;
using OmniToolbox.Teleport;
using OmniToolbox.TreeHouse;
using OmniToolbox.TreeHouse.InterfaceOperation;
using OmniToolbox.TreeHouse.Script;

namespace OmniToolbox.Common.Module.Cloud;

public interface ICloudModulePack
{
    IReadOnlyList<OmniModuleBase> CreateModules(CloudModuleContext context);
}

public interface ICloudModuleActivity
{
    bool IsBusy { get; }
}

public sealed record CloudModuleContext
(
    TreeHouseConfig Settings,
    ITestFeatureAccess Access,
    ICloudTeleportService Teleport,
    ICloudPathService Path,
    IInventorySnapshotReader Inventory,
    ICloudMarketPurchaseService MarketPurchase,
    IAutoStoreService ItemStore,
    NonEntityTargetVisibility TargetVisibility,
    CloudTreasureMapService TreasureMap
)
{
    public CloudInvulnerableState Invulnerability { get; } = new();

    public CloudFreeCompanyActionService FreeCompanyAction { get; } = new();
}

public interface ITestFeatureAccess : IFeatureAccess
{
    bool CanUseTestFeature(out string reason);

    bool CanUseTestFeature(int requiredLevel, out string reason);
}

public interface ICloudTeleportService : ITeleportService
{
    bool IsBusy { get; }

    OperationSnapshot? LatestOperation { get; }

    OperationSnapshot? GetOperation(string operationID);

    bool CancelOperation(string operationID);

    bool TryTeleportToNearest(uint territoryID, System.Numerics.Vector3 position);
}

public interface ICloudPathService
{
    bool IsEnabled { get; }

    bool IsRunning { get; }

    long Revision { get; }

    bool Begin(System.Numerics.Vector3 destination, uint territoryID, float arrivalDistance);

    bool TryFollowPath(IReadOnlyList<System.Numerics.Vector3> waypoints);

    AutoPathStage Tick();

    void Cancel();
}

public interface ICloudMarketPurchaseService
{
    bool IsAvailable { get; }

    bool CanPurchaseInCurrentArea { get; }

    bool IsBusy { get; }

    bool RequestPurchase(uint itemID, bool? isHQ, uint quantity, int maxUnitPrice);

    void Cancel();
}

public sealed class CloudTreasureMapService : ITreasureMapService
{
    private ITreasureMapService? current;
    private Func<bool>? isAutomationRunning;

    public bool IsAvailable => current?.IsAvailable == true;

    public bool IsBusy => current?.IsBusy == true;

    public bool IsAutomationRunning => current is not null && isAutomationRunning?.Invoke() == true;

    public string StatusKey => current?.StatusKey ?? "Status.PrivateProviderUnavailable";

    public bool RequestStart(uint itemID, int maxUnitPrice) =>
        current?.RequestStart(itemID, maxUnitPrice) == true;

    public void Cancel() => current?.Cancel();

    public IDisposable Register(ITreasureMapService service, Func<bool> automationState)
    {
        if (current is not null)
            throw new InvalidOperationException("A cloud treasure map service is already registered.");
        current = service;
        isAutomationRunning = automationState;
        return new Registration(this, service);
    }

    private sealed class Registration(CloudTreasureMapService owner, ITreasureMapService service) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(owner.current, service))
            {
                owner.current = null;
                owner.isAutomationRunning = null;
            }
        }
    }
}
