namespace Shane32.AsyncResetEvents;

/// <summary>
/// An asynchronous delegate pump, where queued asynchronous delegates are
/// executed in order.
/// </summary>
public class AsyncDelegatePump : AsyncMessagePump<IDelegateTuple>
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public AsyncDelegatePump() : base(static info => info.ExecuteAsync())
    {
    }

    /// <summary>
    /// Executes a delegate in order and returns its result.
    /// </summary>
    public Task SendAsync(Func<Task> action)
        => SendAsync(action, Timeout.InfiniteTimeSpan, default);

    /// <summary>
    /// Executes a delegate in order and returns its result.
    /// If the cancellation token is signaled before the delegate
    /// starts executing, the delegate will not be started, and the
    /// task will be canceled.
    /// </summary>
    public Task SendAsync(Func<Task> action, CancellationToken cancellationToken)
        => SendAsync(action, Timeout.InfiniteTimeSpan, cancellationToken);

    /// <summary>
    /// Executes a delegate in order and returns its result.
    /// If the cancellation token is signaled before the delegate
    /// starts executing, the delegate will not be started, and the
    /// task will be canceled.  If the timeout is reached before the
    /// delegate starts executing, the delegate will not be started,
    /// and the task will throw <see cref="TimeoutException"/>.
    /// </summary>
    public Task SendAsync(Func<Task> action, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new DelegateTuple(action, timeout, cancellationToken);
        Post(info);
        return info.Task;
    }

    /// <inheritdoc cref="SendAsync(Func{Task})"/>
    public Task<T> SendAsync<T>(Func<Task<T>> action)
        => SendAsync(action, Timeout.InfiniteTimeSpan, default);

    /// <inheritdoc cref="SendAsync(Func{Task}, CancellationToken)"/>
    public Task<T> SendAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
        => SendAsync(action, Timeout.InfiniteTimeSpan, cancellationToken);

    /// <inheritdoc cref="SendAsync(Func{Task}, TimeSpan, CancellationToken)"/>
    public Task<T> SendAsync<T>(Func<Task<T>> action, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new DelegateTuple<T>(action, timeout, cancellationToken);
        Post(info);
        return info.Task;
    }

    /// <inheritdoc cref="AsyncMessagePump{T}.Post(T)"/>
    public void Post(Func<Task> action)
    {
        Post(new SimpleTuple(action));
    }

    private class SimpleTuple : IDelegateTuple
    {
        private readonly Func<Task> _action;
        public SimpleTuple(Func<Task> action)
        {
            _action = action;
        }
        public Task ExecuteAsync() => _action();
    }
}
