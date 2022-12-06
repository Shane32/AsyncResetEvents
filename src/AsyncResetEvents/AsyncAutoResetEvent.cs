namespace Shane32.AsyncResetEvents;

/// <inheritdoc cref="AutoResetEvent"/>
public sealed class AsyncAutoResetEvent
{
    private static readonly Task<bool> _taskTrue = Task.FromResult(true);
    private static readonly Task<bool> _taskFalse = Task.FromResult(false);
    private readonly Queue<TaskCompletionSource<bool>> _taskCompletionSourceQueue = new();
    private bool _signaled;

    /// <summary>
    /// Returns a task that will complete when the reset event has been signaled.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the reset event to be signaled.</param>
    /// <exception cref="OperationCanceledException">The provided <paramref name="cancellationToken"/> was signaled.</exception>
    /// <exception cref="ObjectDisposedException">The provided <paramref name="cancellationToken"/> has already been disposed.</exception>
    public Task WaitAsync(CancellationToken cancellationToken = default)
        => WaitAsync(-1, cancellationToken);

    /// <summary>
    /// Returns a task that will complete when the reset event has been signaled.
    /// </summary>
    /// <param name="millisecondsTimeout">The number of milliseconds to wait before returning, or -1 to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the reset event to be signaled.</param>
    /// <returns>A task that returns <see langword="true"/> if the reset event was signaled, or <see langword="false"/> if the timeout period expired.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The <paramref name="millisecondsTimeout"/> is less than -1.</exception>
    /// <exception cref="OperationCanceledException">The provided <paramref name="cancellationToken"/> was signaled.</exception>
    /// <exception cref="ObjectDisposedException">The provided <paramref name="cancellationToken"/> has already been disposed.</exception>
    public Task<bool> WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken = default)
    {
        if (millisecondsTimeout < -1)
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
#if NETSTANDARD1_0
        cancellationToken.ThrowIfCancellationRequested();
#else
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<bool>(cancellationToken);
#endif
        Task<bool> task;
        lock (_taskCompletionSourceQueue) {
            if (_signaled) {
                _signaled = false;
                return _taskTrue;
            } else if (millisecondsTimeout == 0) {
                return _taskFalse;
            } else {
                var tcs = new TaskCompletionSource<bool>();
                _taskCompletionSourceQueue.Enqueue(tcs);
                task = tcs.Task;
            }
        }

        if (millisecondsTimeout == -1 && !cancellationToken.CanBeCanceled) {
            return task;
        }

        var t = task.WaitOrFalseAsync(millisecondsTimeout, cancellationToken);
        
        _ = t.ContinueWith(
            static (task1, state) => {
                var state2 = (MyStruct)state!;
                if (task1.IsCanceled || task1.IsFaulted || task1.Result == false) {
                    state2.Task.ContinueWith(
                        static (task2, state) => ((AsyncAutoResetEvent)state!).Set(),
                        state2.AsyncAutoResetEvent,
                        TaskContinuationOptions.ExecuteSynchronously);
                }
            },
            new MyStruct { Task = task, AsyncAutoResetEvent = this },
            CancellationToken.None);

        return t;
    }

    private struct MyStruct
    {
        public Task Task;
        public AsyncAutoResetEvent AsyncAutoResetEvent;
    }

    /// <summary>
    /// Returns a task that will complete when the reset event has been signaled.
    /// </summary>
    /// <param name="timeout">The time span to wait before returning, or <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the reset event to be signaled.</param>
    /// <returns>A task that returns <see langword="true"/> if the reset event was signaled, or <see langword="false"/> if the timeout period expired.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The <paramref name="timeout"/> is less than zero and not <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
    /// <exception cref="OperationCanceledException">The provided <paramref name="cancellationToken"/> was signaled.</exception>
    /// <exception cref="ObjectDisposedException">The provided <paramref name="cancellationToken"/> has already been disposed.</exception>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => WaitAsync((int)timeout.TotalMilliseconds, cancellationToken);

    /// <summary>
    /// Sets the state of the event to signaled.
    /// </summary>
    /// <param name="backgroundThread">Determines whether a waiting task is executed on the current thread or a background thread.</param>
    public void Set(bool backgroundThread = false)
    {
        if (backgroundThread) {
            Task.Run(() => Set(false));
            return;
        }
        TaskCompletionSource<bool>? toRelease = null;
        lock (_taskCompletionSourceQueue) {
            if (_taskCompletionSourceQueue.Count > 0)
                toRelease = _taskCompletionSourceQueue.Dequeue();
            else if (!_signaled)
                _signaled = true;
        }
        toRelease?.SetResult(true);
    }
}
