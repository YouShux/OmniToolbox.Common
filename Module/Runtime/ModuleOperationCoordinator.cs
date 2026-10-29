using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace OmniToolbox.Common.Module.Runtime;

public sealed class ModuleOperationCoordinator : IAsyncDisposable
{
    private static readonly AsyncLocal<ModuleOperationCoordinator?> Current      = new();
    private readonly object                                         syncRoot     = new();
    private readonly Channel<Operation>                             operations;
    private readonly CancellationTokenSource                        stopping     = new();
    private readonly Action<Exception>?                             reportError;
    private readonly Task                                           consumer;
    private Task?                                                   stoppingTask;
    private bool                                                    accepting    = true;
    private int                                                     pendingCount;

    public ModuleOperationCoordinator
    (
        int                capacity = 128,
        Action<Exception>? reportError = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        operations       = Channel.CreateBounded<Operation>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        this.reportError = reportError;
        consumer         = Task.Run(ProcessAsync);
    }

    public int PendingCount => Volatile.Read(ref pendingCount);

    public bool IsBusy => PendingCount > 0;

    internal ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken) =>
        operations.Writer.WaitToWriteAsync(cancellationToken);

    public Task<ModuleOperationResult> Enqueue
    (
        Func<CancellationToken, Task<ModuleOperationResult>> operation,
        CancellationToken                                    cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (ReferenceEquals(Current.Value, this))
            return Task.FromResult(ModuleOperationResult.Failure("A lifecycle callback or managed task cannot await another operation in the same module coordinator."));
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ModuleOperationResult.Cancelled);

        var request = new Operation(operation, cancellationToken);
        lock (syncRoot)
        {
            if (!accepting)
            {
                request.Dispose();
                return Task.FromResult(ModuleOperationResult.Cancelled);
            }

            Interlocked.Increment(ref pendingCount);
            if (!operations.Writer.TryWrite(request))
            {
                Interlocked.Decrement(ref pendingCount);
                request.Dispose();
                return Task.FromResult(ModuleOperationResult.Busy);
            }
        }

        return request.Completion.Task;
    }

    public Task StopAsync()
    {
        if (ReferenceEquals(Current.Value, this))
            return Task.FromException(new InvalidOperationException("A module operation cannot await its own coordinator shutdown."));
        TaskCompletionSource completion;
        lock (syncRoot)
        {
            if (stoppingTask is not null)
                return stoppingTask;

            accepting = false;
            operations.Writer.TryComplete();
            completion   = new(TaskCreationOptions.RunContinuationsAsynchronously);
            stoppingTask = completion.Task;
        }

        _ = CompleteStopAsync(completion);
        return completion.Task;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        stopping.Dispose();
    }

    internal static T RunDetached<T>(Func<T> operation)
    {
        var previous = Current.Value;
        Current.Value = null;
        try
        {
            return operation();
        }
        finally
        {
            Current.Value = previous;
        }
    }

    private async Task ProcessAsync()
    {
        await foreach (var request in operations.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (!request.TryStart())
                    continue;
                if (stopping.IsCancellationRequested)
                {
                    request.Completion.TrySetResult(ModuleOperationResult.Cancelled);
                    continue;
                }

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token, request.CancellationToken);
                var previous = Current.Value;
                try
                {
                    Current.Value = this;
                    var result = await request.Execute(linked.Token).ConfigureAwait(false);
                    request.Completion.TrySetResult(result);
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    request.Completion.TrySetResult(ModuleOperationResult.Cancelled);
                }
                catch (Exception exception)
                {
                    Report(exception);
                    request.Completion.TrySetResult(ModuleOperationResult.Failure(exception.Message));
                }
                finally
                {
                    Current.Value = previous;
                }
            }
            finally
            {
                request.Dispose();
                Interlocked.Decrement(ref pendingCount);
            }
        }
    }

    private async Task CompleteStopAsync(TaskCompletionSource completion)
    {
        try
        {
            try
            {
                await stopping.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Report(exception);
            }

            await consumer.ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void Report(Exception exception)
    {
        try
        {
            if (reportError is { } report)
                report(exception);
            else
                Trace.TraceError(exception.ToString());
        }
        catch (Exception reportException)
        {
            Trace.TraceError(reportException.ToString());
        }
    }

    private sealed class Operation : IDisposable
    {
        private readonly CancellationTokenRegistration cancellationRegistration;
        private int                                    started;

        internal Operation
        (
            Func<CancellationToken, Task<ModuleOperationResult>> execute,
            CancellationToken                                    cancellationToken
        )
        {
            Execute                  = execute;
            CancellationToken        = cancellationToken;
            cancellationRegistration = cancellationToken.Register(() =>
            {
                if (Interlocked.CompareExchange(ref started, -1, 0) == 0)
                    Completion.TrySetResult(ModuleOperationResult.Cancelled);
            });
        }

        internal Func<CancellationToken, Task<ModuleOperationResult>> Execute { get; }

        internal CancellationToken CancellationToken { get; }

        internal TaskCompletionSource<ModuleOperationResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool TryStart() => Interlocked.CompareExchange(ref started, 1, 0) == 0;

        public void Dispose() => cancellationRegistration.Dispose();
    }
}
