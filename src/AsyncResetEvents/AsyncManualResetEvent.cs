namespace Shane32.AsyncResetEvents;

/// <inheritdoc cref="ManualResetEvent"/>
public sealed class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> _taskCompletionSource = new();

    /// <summary>
    /// Initializes a new unsignaled instance.
    /// </summary>
    public AsyncManualResetEvent()
    {
    }

    /// <summary>
    /// Initializes a new instance set to signaled if specified.
    /// </summary>
    public AsyncManualResetEvent(bool signaled)
    {
        if (signaled)
            Set(false);
    }

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
        => _taskCompletionSource.Task.WaitOrFalseAsync(millisecondsTimeout, cancellationToken);

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
    /// <param name="backgroundThread">Determines whether waiting tasks are executed on the current thread or a background thread.</param>
    public void Set(bool backgroundThread = false)
    {
        if (backgroundThread) {
            // Task.Run with state
            _ = Task.Factory.StartNew(
                static mre => ((AsyncManualResetEvent)mre!).Set(false),
                this,
                default,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
            return;
        }
        _taskCompletionSource.TrySetResult(true);
    }

    /// <summary>
    /// Sets the state of the event to nonsignaled.
    /// </summary>
    public void Reset()
    {
        // if the task is completed (aka if the event needs to be reset),
        // swaps the signaled task completion source with a new instance
        while (true) {
            var tcs = _taskCompletionSource;
            if (!tcs.Task.IsCompleted || Interlocked.CompareExchange(
                ref _taskCompletionSource,
                new TaskCompletionSource<bool>(),
                tcs) == tcs)
                return;
        }
    }
}
