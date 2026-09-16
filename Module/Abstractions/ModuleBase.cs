using OmniToolbox.Common.Module.Models;
using OmniToolbox.UI;

namespace OmniToolbox.Common.Module.Abstractions;

public abstract class ModuleBase : IDisposable
{
    private bool disposed;
    private Action<uint>? iconSelection;

    internal IconBrowser? HostIconBrowser { get; set; }

    internal Action? SaveHostConfig { get; set; }

    protected ModuleBase()
    {
        ModuleName = GetType().Name;
    }

    public string ModuleName { get; }

    public abstract ModuleInfo Info { get; }

    public bool IsEnabled { get; private set; }

    public virtual bool HasSettings => false;

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsEnabled == enabled)
        {
            return;
        }

        if (enabled)
        {
            OnEnable();
            IsEnabled = true;
            return;
        }

        try
        {
            OnDisable();
        }
        finally
        {
            CancelIconSelection();
            IsEnabled = false;
        }
    }

    public virtual bool DrawSettings() => false;

    public virtual bool ResetSettings() => false;

    /// <summary>
    /// 处理 /omni 模块类名 后的参数。仅在模块启用时由宿主调用；返回 false 表示不支持该指令。
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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            if (IsEnabled)
            {
                OnDisable();
            }
        }
        finally
        {
            try
            {
                OnDispose();
            }
            finally
            {
                CancelIconSelection();
                HostIconBrowser = null;
                SaveHostConfig = null;
                IsEnabled = false;
                disposed = true;
            }
        }
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

    protected virtual void OnEnable()
    {
    }

    protected virtual void OnDisable()
    {
    }

    protected virtual bool OnInterruptAutomation() => false;

    protected virtual void OnDispose()
    {
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
