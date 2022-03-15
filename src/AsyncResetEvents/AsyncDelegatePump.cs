namespace Shane32.AsyncResetEvents;

/// <summary>
/// An asynchronous delegate pump, where queued asynchronous delegates are
/// executed in order.
/// </summary>
public class AsyncDelegatePump : AsyncMessagePump<Func<Task>>
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public AsyncDelegatePump() : base(d => d())
    {
    }

    /// <summary>
    /// Executes a delegate in order and returns its result.
    /// </summary>
    public Task SendAsync(Func<Task> action)
    {
#if NET5_0_OR_GREATER
        var tcs = new TaskCompletionSource();
#else
        var tcs = new TaskCompletionSource<byte>();
#endif
        Post(() => {
            Task task;
            try {
                task = action();
            } catch (Exception ex) {
                tcs.SetException(ex);
                throw;
            }
            task.ContinueWith(task2 => {
                if (task2.IsFaulted)
                    tcs.SetException(task2.Exception!.GetBaseException());
                else if (task2.IsCanceled)
                    tcs.SetCanceled();
                else
#if NET5_0_OR_GREATER
                    tcs.SetResult();
#else
                    tcs.SetResult(0);
#endif
            }, TaskContinuationOptions.ExecuteSynchronously);
            return task;
        });
        return tcs.Task;
    }

    /// <summary>
    /// Executes a delegate in order and returns its result.
    /// </summary>
    public Task<T> SendAsync<T>(Func<Task<T>> action)
    {
        var tcs = new TaskCompletionSource<T>();
        Post(() => {
            Task<T> task;
            try {
                task = action();
            } catch (Exception ex) {
                tcs.SetException(ex);
                throw;
            }
            task.ContinueWith(task2 => {
                if (task2.IsFaulted)
                    tcs.SetException(task2.Exception!.GetBaseException());
                else if (task2.IsCanceled)
                    tcs.SetCanceled();
                else
                    tcs.SetResult(task2.Result);
            }, TaskContinuationOptions.ExecuteSynchronously);
            return task;
        });
        return tcs.Task;
    }
}
