using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OmenTools;
using OmniToolbox.Lifecycle;
using HookBackend = Dalamud.Plugin.Services.IGameInteropProvider.HookBackend;

namespace OmniToolbox.Host;

internal class PluginInterfaceAdapter : DispatchProxy
{
    private IDalamudPluginInterface source = null!;
    private InteropProvider interopProvider = null!;

    public static IDalamudPluginInterface Create(IDalamudPluginInterface source)
    {
        var proxy = Create<IDalamudPluginInterface, PluginInterfaceAdapter>();
        var instance = (PluginInterfaceAdapter)proxy;
        instance.source = source;
        instance.interopProvider = new InteropProvider(
            source.GetService(typeof(IGameInteropProvider)) as IGameInteropProvider
            ?? throw new InvalidOperationException("Game interop service is unavailable."),
            source.GetService(typeof(ISigScanner)) as ISigScanner
            ?? throw new InvalidOperationException("Signature scanner service is unavailable."));
        return proxy;
    }

    public static void DisposeHooks(IDalamudPluginInterface pluginInterface)
    {
        if (pluginInterface is PluginInterfaceAdapter adapter)
            adapter.interopProvider.Dispose();
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        if (method.Name == nameof(IDalamudPluginInterface.GetService) && args is [Type serviceType] &&
            serviceType == typeof(IGameInteropProvider)) return interopProvider;
        try
        {
            var result = method.Invoke(source, args);
            if (method.Name == nameof(IDalamudPluginInterface.Inject) && result is true && args is [DService service, ..])
            {
                var hookProperty = typeof(DService).GetProperty(nameof(DService.Hook))
                                   ?? throw new MissingMemberException(typeof(DService).FullName, nameof(DService.Hook));
                hookProperty.SetValue(service, interopProvider);
            }

            return result;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private sealed unsafe class InteropProvider(IGameInteropProvider source, ISigScanner scanner) : IGameInteropProvider, IDisposable
    {
        private readonly FeatureLifetime lifetime = new();
        private bool useSafetyHook;

        public void Dispose() => lifetime.Dispose();

        public void InitializeFromAttributes(object self) => source.InitializeFromAttributes(self);

        public Hook<T> HookFromFunctionPointerVariable<T>(nint address, T detour) where T : Delegate =>
            source.HookFromFunctionPointerVariable(address, detour);

        public Hook<T> HookFromImport<T>(ProcessModule? module, string moduleName, string functionName, uint hintOrOrdinal, T detour)
            where T : Delegate => source.HookFromImport(module, moduleName, functionName, hintOrOrdinal, detour);

        public Hook<T> HookFromSymbol<T>(string moduleName, string exportName, T detour, HookBackend backend = HookBackend.Automatic)
            where T : Delegate => source.HookFromSymbol(moduleName, exportName, detour, backend);

        public Hook<T> HookFromAddress<T>(nint procAddress, T detour, HookBackend backend = HookBackend.Automatic) where T : Delegate
        {
            if (useSafetyHook && backend == HookBackend.Automatic)
                return CreateSafetyHook(procAddress, detour);

            try
            {
                return source.HookFromAddress(procAddress, detour, backend);
            }
            catch (Exception exception) when (backend == HookBackend.Automatic &&
                                               exception.Message.StartsWith("Unable to find memory location to fit MemoryBuffer", StringComparison.Ordinal))
            {
                useSafetyHook = true;
                return CreateSafetyHook(procAddress, detour);
            }
        }

        public Hook<T> HookFromAddress<T>(nuint procAddress, T detour, HookBackend backend = HookBackend.Automatic) where T : Delegate =>
            HookFromAddress((nint)procAddress, detour, backend);

        public Hook<T> HookFromAddress<T>(void* procAddress, T detour, HookBackend backend = HookBackend.Automatic) where T : Delegate =>
            HookFromAddress((nint)procAddress, detour, backend);

        public Hook<T> HookFromSignature<T>(string signature, T detour, HookBackend backend = HookBackend.Automatic) where T : Delegate =>
            HookFromAddress(scanner.ScanText(signature), detour, backend);

        private Hook<T> CreateSafetyHook<T>(nint procAddress, T detour) where T : Delegate
        {
            var assembly = typeof(IGameInteropProvider).Assembly;
            var hookManager = assembly.GetType("Dalamud.Hooking.Internal.HookManager", true);
            var followJmp = hookManager?.GetMethod("FollowJmp", BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new MissingMethodException("Dalamud.Hooking.Internal.HookManager", "FollowJmp");
            var safetyHook = assembly.GetType("Dalamud.Hooking.Internal.SafetyHookHook`1", true)?.MakeGenericType(typeof(T))
                             ?? throw new TypeLoadException("Dalamud SafetyHook backend is unavailable.");
            try
            {
                var address = followJmp.Invoke(null, [procAddress]);
                var hook = Activator.CreateInstance(safetyHook, BindingFlags.Instance | BindingFlags.NonPublic,
                                                null, [address, detour, detour.Method.Module.Assembly], null) as Hook<T>
                       ?? throw new InvalidOperationException("SafetyHook initialization failed.");
                lifetime.Add(hook.Dispose);
                return hook;
            }
            catch (TargetInvocationException invocationException) when (invocationException.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                throw;
            }
        }
    }
}
