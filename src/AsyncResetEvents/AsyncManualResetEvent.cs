namespace Shane32.AsyncResetEvents;

/// <inheritdoc cref="ManualResetEvent"/>
public sealed class AsyncManualResetEvent
{
#if NET5_0_OR_GREATER
    private volatile TaskCompletionSource _taskCompletionSource = new();
#else
    private volatile TaskCompletionSource<bool> _taskCompletionSource = new();
#endif

    private static readonly Task<bool> _taskTrue = Task.FromResult(true);
    private static readonly Task<bool> _taskFalse = Task.FromResult(false);

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
    {
        if (!cancellationToken.CanBeCanceled)
            return _taskCompletionSource.Task;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.WhenAny(_taskCompletionSource.Task, Task.Delay(-1, cancellationToken)).Unwrap();
    }

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
        if (millisecondsTimeout < 0)
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
        cancellationToken.ThrowIfCancellationRequested();
        if (millisecondsTimeout == 0) {
            return _taskCompletionSource.Task.IsCompleted ? _taskTrue : _taskFalse;
        }
        if (millisecondsTimeout == -1 && !cancellationToken.CanBeCanceled) {
#if NET5_0_OR_GREATER
            return _taskCompletionSource.Task.ContinueWith(_ => true, TaskContinuationOptions.ExecuteSynchronously);
#else
            return _taskCompletionSource.Task;
#endif
        }
        var resetTask = _taskCompletionSource.Task;
        return Wait();

        async Task<bool> Wait()
        {
            var task = await Task.WhenAny(resetTask, Task.Delay(millisecondsTimeout, cancellationToken)).ConfigureAwait(false);
            await task;
            return resetTask == task;
        }
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
    /// <param name="backgroundThread">Determines whether waiting tasks are executed on the current thread or a background thread.</param>
    public void Set(bool backgroundThread = false)
    {
        if (backgroundThread) {
            Task.Run(() => Set(false));
            return;
        }
#if NET5_0_OR_GREATER
        _taskCompletionSource.TrySetResult();
#else
        _taskCompletionSource.TrySetResult(true);
#endif
    }

    /// <summary>
    /// Sets the state of the event to nonsignaled.
    /// </summary>
    public void Reset()
    {
        while (true) {
            var tcs = _taskCompletionSource;
            if (!tcs.Task.IsCompleted || Interlocked.CompareExchange(
                ref _taskCompletionSource,
#if NET5_0_OR_GREATER
                new TaskCompletionSource(),
#else
                new TaskCompletionSource<bool>(),
#endif
                tcs) == tcs)
                return;
        }
    }
}
