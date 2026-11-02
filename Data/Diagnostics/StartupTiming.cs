using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OmniToolbox.Host;

namespace OmniToolbox.Data;

internal sealed class StartupTiming : IDisposable
{
    private static readonly AsyncLocal<StartupTiming?> Active = new();
    private static int NextID;
    private readonly ConcurrentQueue<StageRecord> stages = new();
    private readonly ConcurrentDictionary<(string Resource, string Operation), ResourceCount> resources = new();
    private readonly StartupTiming? previous;
    private readonly string operation;
    private readonly int id = Interlocked.Increment(ref NextID);
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private string status = "incomplete";
    private int disposed;

    private StartupTiming(string operation)
    {
        this.operation = operation;
        previous = Active.Value;
        Active.Value = this;
    }

    internal static StartupTiming? Current => Active.Value is { } timing && Volatile.Read(ref timing.disposed) == 0 ? timing : null;

    internal static StartupTiming Begin(string operation) => new(operation);

    internal static void Count(string resource, string operation, long count = 1, long bytes = 0)
    {
        var timing = Active.Value;
        if (timing is null)
            return;

        timing.resources.AddOrUpdate((resource, operation), new ResourceCount(count, bytes),
            (_, current) => new(current.Count + count, current.Bytes + bytes));
        if (Volatile.Read(ref timing.disposed) != 0)
            DalamudServices.PluginLog.Debug("Omni startup {RunId} late resource={Resource} operation={Operation} count={Count} bytes={Bytes}",
                timing.id, resource, operation, count, bytes);
    }

    internal T Run<T>(string stage, Func<T> action)
    {
        var previousTiming = Active.Value;
        Active.Value = this;
        var start = Stopwatch.GetTimestamp();
        var outcome = "completed";
        try
        {
            return action();
        }
        catch (Exception exception)
        {
            outcome = exception is OperationCanceledException ? "cancelled" : "failed:" + exception.GetType().Name;
            throw;
        }
        finally
        {
            Record(stage, start, outcome);
            Active.Value = previousTiming;
        }
    }

    internal void Run(string stage, Action action) => Run(stage, () =>
    {
        action();
        return true;
    });

    internal async Task<T> RunAsync<T>(string stage, Func<Task<T>> action)
    {
        var previousTiming = Active.Value;
        Active.Value = this;
        var start = Stopwatch.GetTimestamp();
        var outcome = "completed";
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            outcome = exception is OperationCanceledException ? "cancelled" : "failed:" + exception.GetType().Name;
            throw;
        }
        finally
        {
            Record(stage, start, outcome);
            Active.Value = previousTiming;
        }
    }

    internal Task RunAsync(string stage, Func<Task> action) => RunAsync(stage, async () =>
    {
        await action().ConfigureAwait(false);
        return true;
    });

    internal void Complete() => status = "completed";

    internal void Fail(Exception exception) =>
        status = exception is OperationCanceledException ? "cancelled" : "failed:" + exception.GetType().Name;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        Active.Value = previous;
        if (status == "incomplete" && stages.LastOrDefault(stage => stage.Status != "completed") is { Status: not null } failed)
            status = failed.Status;
        var summary = string.Join("; ", stages.Select(stage =>
            FormattableString.Invariant($"{stage.Name}={stage.ElapsedMilliseconds:F2}ms/{stage.Status}")));
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var build = typeof(StartupTiming).Assembly.ManifestModule.ModuleVersionId.ToString("N")[..12];
        DalamudServices.PluginLog.Information(
            "Omni startup {RunId} {Operation} {Status}: total={ElapsedMilliseconds:F2}ms, build={Build}; stages=[{Stages}]. Parallel stages overlap; total is measured independently.",
            id, operation, status, elapsed, build, summary);
        foreach (var ((resource, resourceOperation), count) in resources.OrderBy(pair => pair.Key.Resource, StringComparer.Ordinal)
                     .ThenBy(pair => pair.Key.Operation, StringComparer.Ordinal))
        {
            DalamudServices.PluginLog.Debug("Omni startup {RunId} resource={Resource} operation={Operation} count={Count} bytes={Bytes}",
                id, resource, resourceOperation, count.Count, count.Bytes);
        }
    }

    private void Record(string name, long start, string outcome)
    {
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        stages.Enqueue(new(name, elapsed, outcome));
        DalamudServices.PluginLog.Debug("Omni startup {RunId} stage={Stage} status={Status} elapsed={ElapsedMilliseconds:F2}ms",
            id, name, outcome, elapsed);
        if (Volatile.Read(ref disposed) != 0)
            DalamudServices.PluginLog.Information("Omni startup {RunId} late stage={Stage} status={Status} elapsed={ElapsedMilliseconds:F2}ms",
                id, name, outcome, elapsed);
    }

    private readonly record struct StageRecord(string Name, double ElapsedMilliseconds, string Status);

    private readonly record struct ResourceCount(long Count, long Bytes);
}
