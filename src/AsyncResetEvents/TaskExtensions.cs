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
}
