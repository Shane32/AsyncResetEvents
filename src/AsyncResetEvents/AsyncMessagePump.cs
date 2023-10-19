namespace Shane32.AsyncResetEvents;

/// <summary>
/// <para>
/// An asynchronous message pump, where messages can be posted
/// on multiple threads and executed sequentially.
/// </para>
/// <para>
/// The callback functions are guaranteed to be executed in the
/// same order that <see cref="Post(T)"/> was called.
/// </para>
/// <para>
/// Callback functions may be synchronous or asynchronous.
/// Since callback functions may execute on the same thread that
/// <see cref="Post(T)"/> was called on, it is not suggested
/// to use long-running synchronous tasks within the callback
/// delegate without the use of <see cref="Task.Yield"/>.
/// </para>
/// </summary>
public class AsyncMessagePump<T>
{
    private struct MessageTuple
    {
        public T? Value;
        public Task<T>? Delegate;
    }

    private readonly Func<T, Task> _callback;
    private readonly Queue<MessageTuple> _queue = new();
#if NET5_0_OR_GREATER
    private TaskCompletionSource? _drainTask;
#else
    private TaskCompletionSource<bool>? _drainTask;
#endif

    /// <summary>
    /// Suppresses the flow of the execution context to the asynchronous callback delegate.
    /// When <see langword="false"/>, the execution context is copied from the first call
    /// to <see cref="Post(T)">Post</see>, and this context persists until the queue has
    /// been depleted.
    /// </summary>
    /// <remarks>
    /// Setting this value to <see langword="true"/> also causes the delegate to always
    /// execute on a new thread-pool thread.
    /// <br/><br/>
    /// This property is ignored for .NET Standard 1.0 and .NET Standard 1.3 TFMs
    /// </remarks>
    public virtual bool SuppressAsyncFlow {
        get => _suppressAsyncFlow;
        set => _suppressAsyncFlow = value;
    }
    private bool _suppressAsyncFlow;

    /// <summary>
    /// Initializes a new instances with the specified asynchronous callback delegate.
    /// </summary>
    public AsyncMessagePump(Func<T, Task> callback)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <summary>
    /// Initializes a new instances with the specified synchronous callback delegate.
    /// </summary>
    public AsyncMessagePump(Action<T> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        _callback = message => {
            callback(message);
#if NETSTANDARD1_0
            return DelegateTuple.CompletedTask;
#else
            return Task.CompletedTask;
#endif
        };
    }

    /// <summary>
    /// Posts the specified message to the message queue.
    /// </summary>
    public void Post(T message)
        => PostCore(new MessageTuple { Value = message });

    /// <summary>
    /// Posts the result of an asynchronous operation to the message queue.
    /// </summary>
    public void Post(Task<T> messageTask)
        => PostCore(new MessageTuple { Delegate = messageTask });

    /// <summary>
    /// Posts the result of an asynchronous operation to the message queue.
    /// </summary>
    private void PostCore(MessageTuple messageTuple)
    {
        bool attach = false;
        lock (_queue) {
            _queue.Enqueue(messageTuple);
            attach = _queue.Count == 1;
            if (attach) {
                _drainTask = null;
            }
        }

        if (attach) {
#if NETSTANDARD2_0_OR_GREATER || NET5_0_OR_GREATER
            if (_suppressAsyncFlow) {
                using (ExecutionContext.SuppressFlow()) {
                    // the follwing is equivalent to Task.Run with state
                    _ = Task.Factory.StartNew(
                        static amp => ((AsyncMessagePump<T>)amp!).CompleteAsync(),
                        this,
                        default,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                }
            } else {
                CompleteAsync();
            }
#else
            CompleteAsync();
#endif
        }
    }

    /// <summary>
    /// Returns the number of messages waiting in the queue.
    /// Includes the message currently being processed, if any.
    /// </summary>
    public int Count {
        get {
            lock (_queue) {
                return _queue.Count;
            }
        }
    }

    /// <summary>
    /// Processes message in the queue until it is empty.
    /// </summary>
    private async void CompleteAsync()
    {
        // grab the message at the start of the queue, but don't remove it from the queue
        MessageTuple messageTuple;
        lock (_queue) {
            // should always successfully peek from the queue here
            messageTuple = _queue.Peek();
        }
        while (true) {
            // process the message
            try {
                var message = messageTuple.Delegate != null
                    ? await messageTuple.Delegate.ConfigureAwait(false)
                    : messageTuple.Value!;
                var callbackTask = _callback(message);
                if (callbackTask != null)
                    await callbackTask.ConfigureAwait(false);
            } catch (Exception ex) {
                try {
                    await HandleErrorAsync(ex).ConfigureAwait(false);
                } catch { }
            }

            // once the message has been passed along, dequeue it
            lock (_queue) {
                var messageTask2 = _queue.Dequeue();
                System.Diagnostics.Debug.Assert(messageTuple.Equals(messageTask2));
                // if the queue is empty, immedately quit the loop, as any new
                // events queued will start CompleteAsync
#if NETSTANDARD2_1 || NETCOREAPP2_0_OR_GREATER
                if (!_queue.TryPeek(out messageTuple!)) {
#if NET5_0_OR_GREATER
                    _drainTask?.SetResult();
#else
                    _drainTask?.SetResult(true);
#endif
                    return;
                }
#else
                if (_queue.Count == 0) {
                    _drainTask?.SetResult(true);
                    return;
                }
                messageTuple = _queue.Peek();
#endif
            }
        }
    }

    /// <summary>
    /// Returns a Task that completes when the message pump is empty.
    /// </summary>
    public Task DrainAsync()
    {
        Task drainTask;
        lock (_queue) {
            if (_queue.Count == 0)
#if NETSTANDARD1_0
                return DelegateTuple.CompletedTask;
#else
                return Task.CompletedTask;
#endif
            drainTask = (_drainTask ??= new()).Task;
        }
        return drainTask;
    }

    /// <summary>
    /// Handles exceptions that occur within the asynchronous message delegate or the callback.
    /// </summary>
    protected virtual Task HandleErrorAsync(Exception exception)
#if NETSTANDARD1_0
        => DelegateTuple.CompletedTask;
#else
        => Task.CompletedTask;
#endif
}
