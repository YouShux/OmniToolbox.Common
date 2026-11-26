using OmniToolbox.TreeHouse.InterfaceOperation;

namespace OmniToolbox.Common.Module.Cloud;

public sealed class CloudInvulnerableState
{
    private Func<bool>? current;

    public bool IsActive => current?.Invoke() == true;

    public IDisposable Register(Func<bool> state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (current is not null)
            throw new InvalidOperationException("A cloud invulnerability state is already registered.");
        current = state;
        return new Registration(this, state);
    }

    private sealed class Registration(CloudInvulnerableState owner, Func<bool> state) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(owner.current, state))
                owner.current = null;
        }
    }
}

public sealed class CloudFreeCompanyActionService : IFreeCompanyActionService
{
    private IFreeCompanyActionService? current;

    public bool TryPurchaseAction(int actionID, out string failureReasonKey)
    {
        if (current is { } service)
            return service.TryPurchaseAction(actionID, out failureReasonKey);
        failureReasonKey = "Status.PrivateProviderUnavailable";
        return false;
    }

    public IDisposable Register(IFreeCompanyActionService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (current is not null)
            throw new InvalidOperationException("A cloud free company action service is already registered.");
        current = service;
        return new Registration(this, service);
    }

    private sealed class Registration(CloudFreeCompanyActionService owner, IFreeCompanyActionService service) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(owner.current, service))
                owner.current = null;
        }
    }
}
