# Shane32.AsyncResetEvents

[![NuGet](https://img.shields.io/nuget/v/Shane32.AsyncResetEvents.svg)](https://www.nuget.org/packages/Shane32.AsyncResetEvents) [![Coverage Status](https://coveralls.io/repos/github/Shane32/AsyncResetEvents/badge.svg?branch=master)](https://coveralls.io/github/Shane32/AsyncResetEvents?branch=master)

## AsyncAutoResetEvent

The methods match similar methods on `AutoResetEvent`:

- `void Set(bool backgroundThread = false)`
- `Task WaitAsync(CancellationToken cancellationToken = default)`
- `Task<bool> WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken = default)`
- `Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)`

Calling `Set` will execute waiting task continuation functions on the same thread by default.
Setting the `backgroundThread` parameter will schedule them through the task scheduler.

`WaitAsync` supports a timeout and cancellation token.  If the cancellation token is triggered
before the reset event is signaled, a `OperationCanceledException` is throw.  If the timeout
occurs, `false` is returned.  If the reset event is signaled, `true` is returned.

As an automatic reset event, calling `WaitAsync` will reset the signal.  Note that a queue
is maintained, and calling `Set` will only allow a single waiting task continuation function
to execute. If no task continuation functions are waiting, calling `Set` one or more times
will allow only the next call to `WaitAsync` to execute.  Additional calls to `WaitAsync`
will wait for an additional call to `Set`.

## AsyncManualResetEvent

The methods are identical to above, with the addition of the `Reset` method.

- `void Set(bool backgroundThread = false)`
- `void Reset()`
- `Task WaitAsync(CancellationToken cancellationToken = default)`
- `Task<bool> WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken = default)`
- `Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)`

As a manual reset event, calling `WaitAsync` will wait until the event is signaled.
Signaling the event with `Set` will result in the event staying signaled until `Reset` is
called.  As such, you may call `WaitAsync` any number of times on a signaled event and
execution will continue.  If the reset event is not signaled, you may call `WaitAsync` any
number of times and they will all wait until `Set` is called before executing.

## AsyncMessagePump

Synchronizes execution of a sequence of asynchronous tasks.  This maintains execution order
while allowing tasks to be posted asynchronously to the queue.  The callback delegate may
be an asynchronous or synchronous delegate.  Messages may be posted directly or an
asynchronous operation that returns a message may be posted to the queue.  Regardless,
execution order is maintained as of the point in time that `Post` was called, and
the callback is never run on multiple threads at once.  If an asynchronous operation
was posted and it throws an exception, or if the callback throws an exception, the exception
is handled by the `HandleErrorAsync` protected method, which can be overridden by a user in
a derived class.  `DrainAsync` is provided to wait for pending messages to be processed.

Constructors:

- `AsyncMessagePump(Func<T, Task> callback)`
- `AsyncMessagePump(Action<T> callback)`

Public methods:

- `void Post(T message)`
- `void Post(Task<T> messageTask)`
- `Task DrainAsync()`

Public properties:

- `int Count { get; }`

Protected methods:

- `Task HandleErrorAsync(Exception exception)`

## AsyncDelegatePump

Synchronizes executes of asynchronous delegates.  Queued delegates executes in the order
that they were queued with a call to `Post`.  This class is derived from `AsyncMessagePump`;
see above for further details.  Also included is `SendAsync`, which will queue an asynchronous
delegate and return its result after it executes (in its turn).  Any further code after
awaiting a `Task` returned by `SendAsync` will not prevent additional delegates in the queue
from executing.  `SendAsync` also accepts an optional timeout and cancellation token parameter.
If the timeout expires before the delegate is executed, a `TimeoutException` is thrown and the
delegate is not executed.  Similarly, if the cancellation token is triggered before the delegate
is executed, a `OperationCanceledException` is thrown and the delegate is not executed.

Public methods:

- `void Post(Func<Task> message)`
- `Task SendAsync(Func<Task> action)`
- `Task SendAsync(Func<Task> action, CancellationToken token)`
- `Task SendAsync(Func<Task> action, TimeSpan timeout, CancellationToken token = default)`
- `Task<T> SendAsync<T>(Func<Task<T>> action)`
- `Task<T> SendAsync<T>(Func<Task<T>> action, CancellationToken token)`
- `Task<T> SendAsync<T>(Func<Task<T>> action, TimeSpan timeout, CancellationToken token = default)`
- `Task DrainAsync()`

Public properties:

- `int Count { get; }`

Protected methods:

- `Task HandleErrorAsync(Exception exception)` (not needed when `SendAsync` is used)

## Credits

Glory to Jehovah, Lord of Lords and King of Kings, creator of Heaven and Earth, who through his Son Jesus Christ,
has reedemed me to become a child of God. -Shane32
