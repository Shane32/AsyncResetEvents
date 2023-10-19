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
        base.SuppressAsyncFlow = true;
    }

    /// <summary>
    /// This setting has no effect within <see cref="AsyncDelegatePump"/>.
    /// The execution context is always cloned from the posting thread onto the delegate,
    /// except when running on the .NET Standard 1.0 or .NET Standard 1.3 TFMs.
    /// </summary>
    public override bool SuppressAsyncFlow {
        get => false;
        set { }
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

#if NETSTANDARD2_0_OR_GREATER || NET5_0_OR_GREATER
        private Task? _returnValue;
        private readonly ExecutionContext? _executionContext = ExecutionContext.Capture();
#endif

        public Task ExecuteAsync()
        {
#if NETSTANDARD2_0_OR_GREATER || NET5_0_OR_GREATER
            if (_executionContext != null) {
                ExecutionContext.Run(
                    _executionContext,
                    static state => {
                        var simpleTuple = (SimpleTuple)state!;
                        simpleTuple._returnValue = simpleTuple._action();
                    },
                    this);
                var ret = _returnValue!;
                _returnValue = null;
                return ret;
            }
#endif
            return _action();
        }
    }
}
