namespace Shane32.AsyncResetEvents;

/// <inheritdoc cref="AutoResetEvent"/>
public sealed class AsyncAutoResetEvent
{
    private static readonly Task<bool> _taskTrue = Task.FromResult(true);
    private static readonly Task<bool> _taskFalse = Task.FromResult(false);
#if NET5_0_OR_GREATER
    private readonly Queue<TaskCompletionSource> _taskCompletionSourceQueue = new();
#else
    private readonly Queue<TaskCompletionSource<bool>> _taskCompletionSourceQueue = new();
#endif
    private bool _signaled;

    /// <summary>
    /// Returns a task that will complete when the reset event has been signaled.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the reset event to be signaled.</param>
    /// <exception cref="OperationCanceledException">The provided <paramref name="cancellationToken"/> was signaled.</exception>
    /// <exception cref="ObjectDisposedException">The provided <paramref name="cancellationToken"/> has already been disposed.</exception>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled) {
            return WaitAsync(-1, cancellationToken);
        }
        lock (_taskCompletionSourceQueue) {
            if (_signaled) {
                _signaled = false;
                return _taskTrue;
            } else {
#if NET5_0_OR_GREATER
                var tcs = new TaskCompletionSource();
#else
                var tcs = new TaskCompletionSource<bool>();
#endif
                _taskCompletionSourceQueue.Enqueue(tcs);
                return tcs.Task;
            }
        }
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
        if (millisecondsTimeout < -1)
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
        cancellationToken.ThrowIfCancellationRequested();
#if NET5_0_OR_GREATER
        Task task;
#else
        Task<bool> task;
#endif
        lock (_taskCompletionSourceQueue) {
            if (_signaled) {
                _signaled = false;
                return _taskTrue;
            } else if (millisecondsTimeout == 0) {
                return _taskFalse;
            } else {
#if NET5_0_OR_GREATER
                var tcs = new TaskCompletionSource();
#else
                var tcs = new TaskCompletionSource<bool>();
#endif
                _taskCompletionSourceQueue.Enqueue(tcs);
                task = tcs.Task;
            }
        }
        if (millisecondsTimeout == -1 && !cancellationToken.CanBeCanceled) {
#if NET5_0_OR_GREATER
            return task.ContinueWith(_ => true, TaskContinuationOptions.ExecuteSynchronously);
#else
            return task;
#endif
        }
        return Wait();

        async Task<bool> Wait()
        {
            Task completionTask;
            try {
                completionTask = await Task.WhenAny(task, Task.Delay(millisecondsTimeout, cancellationToken)).ConfigureAwait(false);
                await completionTask;
            }
            catch {
                // when the queued task completion source completes, immediately trigger the next queued task,
                // since there is no practical way (currently) to remove the task completion source from the queue
                _ = task.ContinueWith(_ => Set(), TaskContinuationOptions.ExecuteSynchronously);
                throw;
            }
            if (completionTask != task) {
                // when the queued task completion source completes, immediately trigger the next queued task,
                // since there is no practical way (currently) to remove the task completion source from the queue
                _ = task.ContinueWith(_ => Set(), TaskContinuationOptions.ExecuteSynchronously);
                return false;
            }
            return true;
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
    /// <param name="backgroundThread">Determines whether a waiting task is executed on the current thread or a background thread.</param>
    public void Set(bool backgroundThread = false)
    {
        if (backgroundThread) {
            Task.Run(() => Set(false));
            return;
        }
#if NET5_0_OR_GREATER
        TaskCompletionSource? toRelease = null;
#else
        TaskCompletionSource<bool>? toRelease = null;
#endif
        lock (_taskCompletionSourceQueue) {
            if (_taskCompletionSourceQueue.Count > 0)
                toRelease = _taskCompletionSourceQueue.Dequeue();
            else if (!_signaled)
                _signaled = true;
        }
        if (toRelease != null) {
#if NET5_0_OR_GREATER
            toRelease.SetResult();
#else
            toRelease.SetResult(true);
#endif
        }
    }
}
