namespace Shane32.AsyncResetEvents;

#if !NETSTANDARD1_0
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
        return TimeoutAfter(task, millisecondsDelay, cancellationToken);

        static async Task<bool> TimeoutAfter(Task<bool> task, int millisecondsDelay, CancellationToken cancellationToken)
        {
#if NET6_0_OR_GREATER
            try {
                return await task.WaitAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken).ConfigureAwait(false);
            } catch (TimeoutException) {
                return false;
            }
#else
            using (var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {

                var completedTask = await Task.WhenAny(task, Task.Delay(millisecondsDelay, timeoutCancellationTokenSource.Token)).ConfigureAwait(false);
                if (completedTask == task) {
                    timeoutCancellationTokenSource.Cancel();
                    return await task.ConfigureAwait(false);  // Very important in order to propagate exceptions
                } else {
                    timeoutCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    return false;
                }
            }
#endif
        }
    }

    private class WaitProxy : IDisposable
    {
        private Timer? _timer;
        private CancellationTokenRegistration _ctsRegistration;
        private readonly TaskCompletionSource<bool> _tcs = new();

        private WaitProxy() { }

        // used by WaitOrFalseAsync
        public static Task<bool> Initialize(Task<bool> task, int millisecondsDelay, CancellationToken cancellationToken)
        {
            var instance = new WaitProxy();

            // if the source task finishes, pass the result to the resulting task
            task.ContinueWith(static (task1, state) => {
                var proxy = (WaitProxy)state!;
                if (task1.IsCanceled)
                    proxy._tcs.TrySetCanceled();
                else if (task1.IsFaulted)
                    proxy._tcs.TrySetException(task1.Exception!.InnerExceptions);
                else
                    proxy._tcs.TrySetResult(task1.Result);
            }, instance, CancellationToken.None);

            // register the timer
            if (millisecondsDelay > 0) {
                instance._timer = new Timer(
                    static state => {
                        var proxy = (WaitProxy)state!;
                        proxy._tcs.TrySetResult(false);
                    },
                    instance,
                    millisecondsDelay,
                    Timeout.Infinite);
            }

            // register the cancellation token
            if (cancellationToken.CanBeCanceled) {
                instance._ctsRegistration = cancellationToken.Register(
                    static state => {
                        var proxy = (WaitProxy)state!;
                        proxy._tcs.TrySetCanceled();
                    }, instance);
            }

            // when the resulting task finishes, dispose of the timer and cancellation token registration
            instance._tcs.Task.ContinueWith(static (_, state) => {
                var proxy = (WaitProxy)state!;
                proxy.Dispose();
            }, instance, CancellationToken.None);

            return instance._tcs.Task;
        }

        public void Dispose()
        {
            // stop/unregister timer
            _timer?.Dispose();
            // unregister cancellation
            _ctsRegistration.Dispose();
        }
    }

    /*
    private class WaitProxy2 : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly TaskCompletionSource<bool> _tcs = new();

        private WaitProxy2(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        // used by WaitOrFalseAsync
        public static Task<bool> Initialize(Task<bool> task, int millisecondsDelay, CancellationToken cancellationToken)
        {
            var instance = new WaitProxy2(cancellationToken);

            // if the source task finishes, pass the result to the resulting task
            task.ContinueWith(static (task1, state) => {
                var proxy = (WaitProxy2)state!;
                if (task1.IsCanceled)
                    proxy._tcs.TrySetCanceled();
                else if (task1.IsFaulted)
                    proxy._tcs.TrySetException(task1.Exception!.InnerExceptions);
                else
                    proxy._tcs.TrySetResult(task1.Result);
            }, instance, CancellationToken.None);

            // register the timer
            var tDelay = Task.Delay(millisecondsDelay, instance._cts.Token);
            tDelay.ContinueWith(static (task1, state) => {
                var proxy = (WaitProxy2)state!;
                proxy._tcs.TrySetResult(false);
            }, instance, CancellationToken.None);

            // when the resulting task finishes, dispose of the timer and cancellation token registration
            instance._tcs.Task.ContinueWith(static (_, state) => {
                var proxy = (WaitProxy2)state!;
                proxy.Dispose();
            }, instance, CancellationToken.None);

            return Task.WhenAny(instance._tcs.Task, tDelay.ContinueWith(_ => false, TaskContinuationOptions.OnlyOnRanToCompletion)).Unwrap();
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
    */
}
#endif
