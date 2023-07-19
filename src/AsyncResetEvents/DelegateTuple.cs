namespace Shane32.AsyncResetEvents;

/// <summary>
/// Holds state information for <see cref="AsyncDelegatePump"/>.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
internal sealed class DelegateTuple : IDelegateTuple
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly Func<Task> _action;

#if NET5_0_OR_GREATER
    private readonly TaskCompletionSource _taskCompletionSource = new();
#else
    private readonly TaskCompletionSource<byte> _taskCompletionSource = new();
#endif

    private readonly CancellationTokenRegistration? _cancellationTokenRegistration;
#if !NETSTANDARD1_0
    private readonly Timer? _timer;
#endif
    private int _started;

#if NETSTANDARD1_0
    internal static readonly Task CompletedTask = Task.FromResult<byte>(0);
#endif

    internal Task Task => _taskCompletionSource.Task;

    public DelegateTuple(Func<Task> action, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (timeout == TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        _action = action;

        if (cancellationToken.CanBeCanceled) {
            // register cancellation callback that will cancel the task if it hasn't started yet
            _cancellationTokenRegistration = cancellationToken.Register(static state => {
                var info = (DelegateTuple)state!;
                if (Interlocked.Exchange(ref info._started, 1) == 0) {
#if !NETSTANDARD1_0
                    info._timer?.Dispose();
#endif
                    // we don't need to dispose the registration because it's already been invoked
                    info._taskCompletionSource.SetCanceled();
                }
            }, this);
        }

        if (timeout != Timeout.InfiniteTimeSpan) {
#if !NETSTANDARD1_0
            _timer = new Timer(static state => {
                var info = (DelegateTuple)state!;
                if (Interlocked.Exchange(ref info._started, 1) == 0) {
                    info._timer?.Dispose();
                    info._cancellationTokenRegistration?.Dispose();
                    info._taskCompletionSource.SetException(new TimeoutException());
                }
            }, this, timeout, Timeout.InfiniteTimeSpan);
#else
            Task.Delay(timeout, default).ContinueWith(static (t, state) => {
                var info = (DelegateTuple)state!;
                if (Interlocked.Exchange(ref info._started, 1) == 0) {
                    info._cancellationTokenRegistration?.Dispose();
                    info._taskCompletionSource.SetException(new TimeoutException());
                }
            }, this, default(CancellationToken));
#endif
        }
    }

    public Task ExecuteAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) {
            // already canceled, so do nothing
#if NETSTANDARD1_0
            return CompletedTask;
#else
            return Task.CompletedTask;
#endif
        }
#if !NETSTANDARD1_0
        _timer?.Dispose();
#endif
        _cancellationTokenRegistration?.Dispose();
        Task task;
        try {
            task = _action.Invoke();
        } catch (Exception ex) {
            _taskCompletionSource.SetException(ex);
            throw;
        }
        task.ContinueWith(static (task2, state) => {
            var info = (DelegateTuple)state!;
            if (task2.IsFaulted)
                info._taskCompletionSource.SetException(task2.Exception!.GetBaseException());
            else if (task2.IsCanceled)
                info._taskCompletionSource.SetCanceled();
            else
#if NET5_0_OR_GREATER
                info._taskCompletionSource.SetResult();
#else
                info._taskCompletionSource.SetResult(0);
#endif
        }, this, TaskContinuationOptions.ExecuteSynchronously);
        return task;
    }
}
