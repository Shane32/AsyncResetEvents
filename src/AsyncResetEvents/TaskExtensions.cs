namespace Shane32.AsyncResetEvents;

internal static class TaskExtensions
{
    private static readonly Task<bool> _falseTask = Task.FromResult(false);

    public static Task<bool> WaitOrFalseAsync(this Task<bool> task, int millisecondsDelay, CancellationToken cancellationToken)
    {
        if (millisecondsDelay < -1)
            throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));
        if (task.IsCompleted || (millisecondsDelay == -1 && !cancellationToken.CanBeCanceled))
            return task;
        if (millisecondsDelay == 0)
            return _falseTask;
#if NETSTANDARD1_0
        cancellationToken.ThrowIfCancellationRequested();
#else
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<bool>(cancellationToken);
#endif
        if (millisecondsDelay > 0) {
            var timer = new System.Threading.Timer(
                callback: static state => {
                    //timer.DisposeAsync //3.0+
                    timer.Dispose();
                },
                state: null,
                dueTime: millisecondsDelay,
                period: Timeout.Infinite);
        }
        // todo: wire up timer
        // todo: wire up cancellation token
        return null!;
    }

    private class WaitProxy
    {
        private Timer? _timer;
        private CancellationTokenRegistration _ctsRegistration;

        private WaitProxy() { }

        public static Task Initialize(Task task, int millisecondsDelay, CancellationToken cancellationToken)
        {
            var instance = new WaitProxy();
            var newTask = task.ContinueWith<bool>(
                static (completedTask, state) => {
                    ((WaitProxy)state).Dispose();
                    return true;
                },
                instance,
                TaskContinuationOptions.ExecuteSynchronously);

            if (millisecondsDelay > 0) {
                instance._timer = new Timer(
                    static state => {

                    },
                    instance,
                    millisecondsDelay,
                    Timeout.Infinite);
            }
            if (cancellationToken.CanBeCanceled) {
                instance._ctsRegistration = cancellationToken.Register(
                    static state => {
                        ((WaitProxy)state).Dispose();
                        newTask.c
                    },
                    instance);
            }
            _task = task;
            _timer = timer;
            _cts = cts;
        }

        private void Dispose()
        {
            // stop timer
            _timer?.Dispose();
            // unregister cancellation
            _ctsRegistration.Dispose();
        }
    }
}
