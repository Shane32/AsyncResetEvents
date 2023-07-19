namespace Shane32.AsyncResetEvents;

/// <inheritdoc cref="AutoResetEvent"/>
public sealed class AsyncAutoResetEvent
{
    // this is a singleton representing 'true' that we can return when the reset event is signaled
    private static readonly Task<bool> _taskTrue = Task.FromResult(true);
    // this is a singleton representing 'false' that we can return when the reset event is not signaled
    // and the timeout period is zero
    private static readonly Task<bool> _taskFalse = Task.FromResult(false);
    // this is the queue of tasks that are waiting on the reset event (via WaitAsync)
    private readonly Queue<TaskCompletionSource<bool>> _taskCompletionSourceQueue = new();
    // this is the flag that indicates whether the reset event is signaled when there are no
    // tasks in the queue
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
        // validate arguments
        if (millisecondsTimeout < -1)
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));

        // if the cancellation token is signaled, throw immediately
#if NETSTANDARD1_0
        cancellationToken.ThrowIfCancellationRequested();
#else
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<bool>(cancellationToken);
#endif

        Task<bool> task;
        lock (_taskCompletionSourceQueue) {
            // if the reset event is already signaled, return a singleton representing
            // 'true' and clear the signal
            if (_signaled) {
                _signaled = false;
                return _taskTrue;
            } else if (millisecondsTimeout == 0) {
                // if there's a zero timeout, immediately return a singleton representing 'false'
                return _taskFalse;
            } else {
                // we need to wait on a signal, so create a task completion source
                // and enqueue it so it will be signaled when the reset event is set
                var tcs = new TaskCompletionSource<bool>();
                _taskCompletionSourceQueue.Enqueue(tcs);
                task = tcs.Task;
            }
        }

        // if there's no timeout and no cancellation token, just return the task
        if (millisecondsTimeout == -1 && !cancellationToken.CanBeCanceled) {
            return task;
        }

#if NET6_0_OR_GREATER
        // otherwise, we need to wait on the task and the timeout/cancellation token
        return WaitOrFalseAsync(task, millisecondsTimeout, cancellationToken);

        async Task<bool> WaitOrFalseAsync(Task task, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            try {
                // wait for the event to be signaled, or the timeout to expire, or the cancellation token to be signaled
                await task.WaitAsync(TimeSpan.FromTicks(millisecondsTimeout * TimeSpan.TicksPerMillisecond), cancellationToken).ConfigureAwait(false);

                // if the reset event is signaled, return true
                return true;
            } catch (TimeoutException) {
                // if a timeout occurs, then when the reset event is signaled, we need to trigger the next task in the queue
                _ = task.ContinueWith(
                    static (task2, state) => ((AsyncAutoResetEvent)state!).Set(),
                    this,
                    TaskContinuationOptions.ExecuteSynchronously);

                // return false indicating that the event was not signaled within the timeout period
                return false;
            } catch (OperationCanceledException) {
                // if the cancellation token is signaled, then when the reset event is signaled, we need to trigger the next task in the queue
                _ = task.ContinueWith(
                    static (task2, state) => ((AsyncAutoResetEvent)state!).Set(),
                    this,
                    TaskContinuationOptions.ExecuteSynchronously);
                // throw the cancellation exception
                throw;
            }
        }
#else
        // otherwise, we need to wait on the task and the timeout/cancellation token
        var t = task.WaitOrFalseAsync(millisecondsTimeout, cancellationToken);

        // if the timeout/cancellation token is triggered, then when the reset
        // event is signaled, we need to trigger the next task in the queue,
        // or if none are in the queue, set _signaled
        _ = t.ContinueWith(
            static (task1, state) => {
                var state2 = (TaskContinuationState)state!;
                if (task1.IsCanceled || task1.IsFaulted || task1.Result == false) {
                    state2.Task.ContinueWith(
                        static (task2, state) => ((AsyncAutoResetEvent)state!).Set(),
                        state2.AsyncAutoResetEvent,
                        TaskContinuationOptions.ExecuteSynchronously);
                }
            },
            new TaskContinuationState(task, this),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return t;
#endif
    }

#if !NET6_0_OR_GREATER
    private sealed class TaskContinuationState
    {
        public Task Task { get; }
        public AsyncAutoResetEvent AsyncAutoResetEvent { get; }

        public TaskContinuationState(Task task, AsyncAutoResetEvent asyncAutoResetEvent)
        {
            Task = task;
            AsyncAutoResetEvent = asyncAutoResetEvent;
        }
    }
#endif

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
        // normally, any code waiting on this AutoResetEvent gets run
        // synchronously on the current thread, but if backgroundThread
        // is true, then we'll run the waiting code on a background thread
        if (backgroundThread) {
            // if there's no waiting code, then we can just return
            lock (_taskCompletionSourceQueue) {
                if (_taskCompletionSourceQueue.Count == 0) {
                    _signaled = true;
                    return;
                }
            }

            // otherwise, we need to run the waiting code on a background thread
            // the follwing is equivalent to Task.Run with state
            _ = Task.Factory.StartNew(
                static mre => ((AsyncAutoResetEvent)mre!).Set(false),
                this,
                default,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            return;
        }

        // dequeue the next task completion source and execute the
        // waiting code on the current thread -- but if there is no
        // waiting code, then we just set _signaled
        TaskCompletionSource<bool> toRelease;
        lock (_taskCompletionSourceQueue) {
            if (_taskCompletionSourceQueue.Count > 0)
                toRelease = _taskCompletionSourceQueue.Dequeue();
            else {
                _signaled = true;
                return;
            }
        }
        toRelease.SetResult(true);
    }
}
