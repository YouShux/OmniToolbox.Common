using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OmenTools;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Common.Module.Runtime;
using TreeTools.Lifecycle;
using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Abstractions;

/// <summary>
/// Omni 模块的统一契约。宿主串行协调启停、资源回收及最终异步释放。
/// </summary>
public abstract class OmniModuleBase : IAsyncDisposable
{
    public abstract ModuleInfo Info { get; }

    private readonly List<OmniModuleBase> ownedChildren = [];
    private readonly List<IDisposable> childRegistrations = [];
    private bool disposed;
    private readonly object disposalLock = new();
    private bool disposing;
    private Task? disposalTask;
    private FeatureLifetime? activation;
    private FeatureLifetime? instanceLifetime;
    private bool activationDisabled;
    private Action<uint>? iconSelection;

    internal OmniLoc.ModuleRegistration? LocalizationRegistration { get; set; }

    internal IconBrowser? HostIconBrowser { get; set; }

    internal Action? SaveHostConfig { get; set; }

    internal Func<bool, bool, CancellationToken, Task<ModuleOperationResult>>? StateChangeRequest { get; set; }

    internal Func<Action, Task> Dispatch { get; set; } = static action => DService.Instance().Framework.Run(action, cancellationToken: CancellationToken.None);

    internal Action<Exception>? ErrorReporter { get; set; }

    public virtual string ModuleName => GetType().Name;

    /// <summary>
    /// 获取必填的模块简体词条；繁体词条由 Omni 自动生成。
    /// </summary>
    public abstract ModuleLocalization Localization { get; }

    public string Title => OmniLoc.Get(this, "Title");

    public string Description => OmniLoc.Get(this, "Description");

    protected FeatureLifetime Activation => activation
        ?? throw new InvalidOperationException("The module has no active lifetime.");

    /// <summary>托管实例存续期间允许在模块停用时执行的工作，最终销毁前取消并等待。</summary>
    protected FeatureLifetime InstanceLifetime
    {
        get
        {
            if (disposing || disposed)
                throw new ObjectDisposedException(ModuleName);
            return instanceLifetime ??= new();
        }
    }

    public bool IsEnabled { get; private set; }

    internal bool IsDisposing => disposing && !disposed;

    internal bool CanHandleCommands => IsEnabled && !disposing && !disposed && activation is { IsStopping: false };

    internal event Action<OmniModuleBase>? EnabledChanged;

    public virtual bool HasSettings => false;

    internal async Task SetEnabledAsync(bool enabled, Func<Action, Task> dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (disposing)
            throw new InvalidOperationException("The module is being disposed.");
        if (IsEnabled == enabled && (enabled || activation is null))
            return;

        if (!enabled)
        {
            await DisableAsync(dispatcher).ConfigureAwait(false);
            return;
        }

        try
        {
            await dispatcher(Enable).ConfigureAwait(false);
            await (activation ?? throw new InvalidOperationException("Module startup did not create an activation.")).WaitForStartupAsync().ConfigureAwait(false);
            await dispatcher(() =>
            {
                IsEnabled = true;
                NotifyEnabledChanged();
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var errors = new List<Exception> { exception };
            await CaptureAsync(errors, () => DisableAsync(dispatcher)).ConfigureAwait(false);
            throw new AggregateException("The module failed to enable.", errors);
        }
    }

    public virtual bool DrawSettings() => false;

    public virtual bool ResetSettings() => false;

    /// <summary>
    /// 处理命令定义中的功能参数。仅在模块启用时由宿主调用；返回 false 表示不支持该指令。
    /// </summary>
    public virtual bool TryHandleCommand(string arguments) => false;

    public virtual bool TryHandleCommand(string command, string arguments) => TryHandleCommand(arguments);

    public bool InterruptAutomation()
    {
        if (!disposed)
        {
            return OnInterruptAutomation();
        }

        return false;
    }

    public ValueTask DisposeAsync() => new(DisposeAsync(Dispatch));

    internal Task DisposeAsync(Func<Action, Task> dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        TaskCompletionSource completion;
        lock (disposalLock)
        {
            if (disposalTask is not null)
                return disposalTask;
            if (disposed)
                return Task.CompletedTask;

            disposing = true;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            disposalTask = completion.Task;
        }

        _ = CompleteDisposalAsync(completion, dispatcher);
        return completion.Task;
    }

    /// <summary>
    /// 登记父实例拥有的私有子模块及词条，最终释放父实例时一并异步释放。
    /// </summary>
    protected void OwnChild(OmniModuleBase child)
    {
        ModuleContract.ValidateType(child.GetType());
        child.Dispatch = Dispatch;
        var registration = OmniLoc.RegisterModule(child);
        ownedChildren.Add(child);
        childRegistrations.Add(registration);
    }

    protected Task SetChildEnabledAsync(OmniModuleBase child, bool enabled) => child.SetEnabledAsync(enabled, Dispatch);

    protected void RequestEnabled(bool enabled, bool affectConfig = false)
    {
        var request = StateChangeRequest
            ?? throw new InvalidOperationException("The host has not provided module operation requests.");
        _ = ObserveRequestAsync(ModuleOperationCoordinator.RunDetached(
            () => request(enabled, affectConfig, CancellationToken.None)));
    }

    protected void OpenIconBrowser(Action<uint> onSelected)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(onSelected);
        var browser = HostIconBrowser
            ?? throw new InvalidOperationException("The host has not provided an icon browser for this module.");
        iconSelection = iconID =>
        {
            onSelected(iconID);
            SaveHostConfig?.Invoke();
        };
        browser.OpenForSelection(false, iconSelection, null);
    }

    /// <summary>
    /// 接入本次启用的事件和资源。取得资源后立即登记到 activation。
    /// </summary>
    protected virtual void OnStart(FeatureLifetime activation)
    {
    }

    /// <summary>
    /// 本次启用的生产入口停止、托管任务结束后执行收尾。
    /// </summary>
    protected virtual void OnStop()
    {
    }

    protected virtual bool OnInterruptAutomation() => false;

    /// <summary>
    /// 实例最终释放时执行收尾，普通停用不会调用。
    /// </summary>
    protected virtual void OnDestroy()
    {
    }

    private void Enable()
    {
        if (activation is not null)
            throw new InvalidOperationException("The previous activation has not been cleaned up.");

        activation = new();
        activationDisabled = false;
        OnStart(activation);
    }

    private async Task DisableAsync(Func<Action, Task> dispatcher)
    {
        if (activation is not { } scope)
            return;

        var errors = new List<Exception>();
        await CaptureAsync(errors, () => scope.StopAsync(dispatcher)).ConfigureAwait(false);
        await CaptureAsync(errors, scope.DrainAsync).ConfigureAwait(false);
        await CaptureAsync(errors, () => dispatcher(DisableHook)).ConfigureAwait(false);
        await CaptureAsync(errors, () => scope.DisposeAsync(dispatcher)).ConfigureAwait(false);
        await dispatcher(() => FinishDisable(errors)).ConfigureAwait(false);
    }

    private void FinishDisable(List<Exception> errors)
    {
        var changed = IsEnabled;
        IsEnabled = false;
        Capture(errors, CancelIconSelection);
        if (errors.Count == 0)
            activation = null;
        if (changed)
            NotifyEnabledChanged();
        if (errors.Count > 0)
            throw new AggregateException("The module failed to disable.", errors);
    }

    private void DisableHook()
    {
        if (activationDisabled)
            return;
        activationDisabled = true;
        OnStop();
    }

    private async Task CompleteDisposeAsync(Func<Action, Task> dispatcher)
    {
        var errors = new List<Exception>();
        if (instanceLifetime is { } instanceScope)
        {
            await CaptureAsync(errors, () => instanceScope.StopAsync(dispatcher)).ConfigureAwait(false);
            if (activation is { } activeScope)
                await CaptureAsync(errors, () => activeScope.StopAsync(dispatcher)).ConfigureAwait(false);
            await CaptureAsync(errors, instanceScope.DrainAsync).ConfigureAwait(false);
        }
        await CaptureAsync(errors, () => DisableAsync(dispatcher)).ConfigureAwait(false);
        for (var index = ownedChildren.Count - 1; index >= 0; index--)
            await CaptureAsync(errors, () => ownedChildren[index].DisposeAsync(dispatcher)).ConfigureAwait(false);
        foreach (var registration in childRegistrations)
            Capture(errors, registration.Dispose);
        ownedChildren.Clear();
        childRegistrations.Clear();
        await CaptureAsync(errors, () => dispatcher(OnDestroy)).ConfigureAwait(false);
        if (instanceLifetime is { } finalScope)
            await CaptureAsync(errors, () => finalScope.DisposeAsync(dispatcher)).ConfigureAwait(false);
        await CaptureAsync(errors, () => dispatcher(CompleteDisposal)).ConfigureAwait(false);
        if (errors.Count > 0)
            throw new AggregateException("The module failed to dispose.", errors);
    }

    private async Task CompleteDisposalAsync(TaskCompletionSource completion, Func<Action, Task> dispatcher)
    {
        try
        {
            await CompleteDisposeAsync(dispatcher).ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void CompleteDisposal()
    {
        try
        {
            CancelIconSelection();
        }
        finally
        {
            LocalizationRegistration = null;
            HostIconBrowser = null;
            SaveHostConfig = null;
            StateChangeRequest = null;
            ErrorReporter = null;
            EnabledChanged = null;
            IsEnabled = false;
            disposed = true;
        }
    }

    private void NotifyEnabledChanged()
    {
        if (EnabledChanged is not { } handlers)
            return;
        foreach (Action<OmniModuleBase> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this);
            }
            catch (Exception exception)
            {
                ReportError(exception);
            }
        }
    }

    private async Task ObserveRequestAsync(Task<ModuleOperationResult> request)
    {
        try
        {
            var result = await request.ConfigureAwait(false);
            if (!result.Succeeded)
                ReportError(new InvalidOperationException(result.Reason));
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private void ReportError(Exception exception)
    {
        try
        {
            if (ErrorReporter is { } report)
                report(exception);
            else
                Trace.TraceError(exception.ToString());
        }
        catch (Exception reportError)
        {
            Trace.TraceError(reportError.ToString());
        }
    }

    private static void Capture(List<Exception> errors, Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private static async Task CaptureAsync(List<Exception> errors, Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private void CancelIconSelection()
    {
        if (iconSelection is not null)
        {
            HostIconBrowser?.CancelSelection(iconSelection);
            iconSelection = null;
        }
    }
}
