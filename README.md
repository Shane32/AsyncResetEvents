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
