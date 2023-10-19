namespace AsyncResetEventTests;

public class AsyncDelegatePumpTests
{
    private readonly StringBuilder _sb = new();
    private readonly AsyncManualResetEvent _reset = new();
    private readonly AsyncDelegatePump _pump = new();

    private void WriteLine(string str)
    {
        lock (_sb)
            _sb.Append(str + " ");
    }

    private void Verify(string expected)
    {
        lock (_sb)
            Assert.Equal(expected, _sb.ToString());
    }

    [Fact]
    public async Task Basic()
    {
        var al = new AsyncLocal<string?>();
        al.Value = "a";
        _pump.Post(async () => {
            await Task.Delay(100);
            Assert.Equal("a", al.Value);
            WriteLine("100");
        });
        al.Value = "b";
        _pump.Post(async () => {
            Verify("100 ");
            await Task.Delay(1);
            Assert.Equal("b", al.Value);
            WriteLine("1");
            _reset.Set();
        });
        al.Value = null;
        Verify("");
        await _reset.WaitAsync();
        Verify("100 1 ");
    }

    [Fact]
    public async Task Send()
    {
        var task1 = _pump.SendAsync(async () => {
            await Task.Delay(100);
            WriteLine("100");
        });
        Assert.False(task1.IsCompleted);
        var task2 = _pump.SendAsync(async () => {
            Verify("100 ");
            await Task.Delay(50);
            WriteLine("50");
        });
        Assert.False(task2.IsCompleted);
        Verify("");
        await task1;
        Assert.True(task1.IsCompleted);
        Assert.False(task2.IsCompleted);
        Verify("100 ");
        await task2;
        Verify("100 50 ");
    }

    [Fact]
    public async Task SendValues()
    {
        var task1 = _pump.SendAsync(async () => {
            await Task.Delay(100);
            WriteLine("100");
            return 1;
        });
        Assert.False(task1.IsCompleted);
        var task2 = _pump.SendAsync(async () => {
            Verify("100 ");
            await Task.Delay(50);
            WriteLine("50");
            return 2;
        });
        Assert.False(task2.IsCompleted);
        Verify("");
        Assert.Equal(1, await task1);
        Assert.True(task1.IsCompleted);
        Assert.False(task2.IsCompleted);
        Verify("100 ");
        Assert.Equal(2, await task2);
        Verify("100 50 ");
    }

    [Fact]
    public async Task ValidateCancellation1()
    {
        var tcs = new TaskCompletionSource<bool>();
        tcs.SetCanceled();
        var task1 = _pump.SendAsync(async () => await Task.Delay(100));
        var task2 = _pump.SendAsync(() => (Task)tcs.Task);
        await task1;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);
    }

    [Fact]
    public async Task ValidateCancellation2()
    {
        var tcs = new TaskCompletionSource<bool>();
        tcs.SetCanceled();
        var task1 = _pump.SendAsync(async () => await Task.Delay(100));
        var task2 = _pump.SendAsync(() => tcs.Task);
        await task1;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);
    }

    [Fact]
    public async Task ValidateException1()
    {
        var task1 = _pump.SendAsync(async () => await Task.Delay(100));
        var task2 = _pump.SendAsync(async () => {
            await Task.Yield();
            throw new ApplicationException("test");
        });
        await task1;
        await Assert.ThrowsAnyAsync<ApplicationException>(() => task2);
    }

    [Fact]
    public async Task ValidateException2()
    {
        var task1 = _pump.SendAsync(async () => await Task.Delay(100));
        var task2 = _pump.SendAsync<bool>(async () => {
            await Task.Yield();
            throw new ApplicationException("test");
        });
        await task1;
        await Assert.ThrowsAnyAsync<ApplicationException>(() => task2);
    }

    [Fact]
    public async Task ValidateException3()
    {
        var task1 = _pump.SendAsync(async () => await Task.Delay(100));
        var task2 = _pump.SendAsync(() => throw new ApplicationException("test"));
        await task1;
        await Assert.ThrowsAnyAsync<ApplicationException>(() => task2);
    }

    [Fact]
    public async Task ValidateException4()
    {
        var task1 = _pump.SendAsync(async () => await Task.Delay(100));
        var task2 = _pump.SendAsync<bool>(() => throw new ApplicationException("test"));
        await task1;
        await Assert.ThrowsAnyAsync<ApplicationException>(() => task2);
    }

    [Fact]
    public async Task CancelsBeforeStarts()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var task1 = _pump.SendAsync(() => (Task)tcs1.Task);
        var cts = new CancellationTokenSource();
        var task2 = _pump.SendAsync(async () => throw new ApplicationException("test"), cts.Token);
        Assert.False(task2.IsCompleted);
        cts.Cancel();
        Assert.True(task2.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);
        Assert.False(task1.IsCompleted);
        tcs1.SetResult(true);
        await task1;
    }

    [Fact]
    public async Task CancelsBeforeStarts_Typed()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var task1 = _pump.SendAsync(() => (Task)tcs1.Task);
        var cts = new CancellationTokenSource();
        var task2 = _pump.SendAsync<bool>(async () => throw new ApplicationException("test"), cts.Token);
        Assert.False(task2.IsCompleted);
        cts.Cancel();
        Assert.True(task2.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);
        Assert.False(task1.IsCompleted);
        tcs1.SetResult(true);
        await task1;
    }

    [Fact]
    public async Task TimeoutBeforeStarts()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var task1 = _pump.SendAsync(() => tcs1.Task);
        var task2 = _pump.SendAsync(async () => throw new ApplicationException("test"), TimeSpan.FromMilliseconds(100));
        Assert.False(task2.IsCompleted);
        await Task.Delay(5000);
        Assert.True(task2.IsCompleted);
        await Assert.ThrowsAnyAsync<TimeoutException>(() => task2);
        Assert.False(task1.IsCompleted);
        tcs1.SetResult(true);
        await task1;
    }

    [Fact]
    public async Task TimeoutBeforeStarts_Typed()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var task1 = _pump.SendAsync(() => (Task)tcs1.Task);
        var task2 = _pump.SendAsync<bool>(async () => throw new ApplicationException("test"), TimeSpan.FromMilliseconds(100));
        Assert.False(task2.IsCompleted);
        await Task.Delay(5000);
        Assert.True(task2.IsCompleted);
        await Assert.ThrowsAnyAsync<TimeoutException>(() => task2);
        Assert.False(task1.IsCompleted);
        tcs1.SetResult(true);
        await task1;
    }

    [Fact]
    public async Task WithTimeoutAndCancellation()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var task1 = _pump.SendAsync(() => (Task)tcs1.Task);
        var tcs2 = new TaskCompletionSource<bool>();
        var cts = new CancellationTokenSource();
        var task2 = _pump.SendAsync(() => (Task)tcs2.Task, TimeSpan.FromMilliseconds(5000), cts.Token);
        Assert.False(task2.IsCompleted);
        Assert.False(task1.IsCompleted);
        tcs1.SetResult(true);
        await task1;
        Assert.False(task2.IsCompleted);
        tcs2.SetResult(true);
        await task2;
    }

    [Fact]
    public async Task WithTimeoutAndCancellation_Typed()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var task1 = _pump.SendAsync(() => (Task)tcs1.Task);
        var tcs2 = new TaskCompletionSource<bool>();
        var cts = new CancellationTokenSource();
        var task2 = _pump.SendAsync(() => tcs2.Task, TimeSpan.FromMilliseconds(5000), cts.Token);
        Assert.False(task2.IsCompleted);
        Assert.False(task1.IsCompleted);
        tcs1.SetResult(true);
        await task1;
        Assert.False(task2.IsCompleted);
        tcs2.SetResult(true);
        Assert.True(await task2);
    }

    [Fact]
    public async Task InvalidTimeoutThrows()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _pump.SendAsync(() => Task.CompletedTask, TimeSpan.FromMilliseconds(-2)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _pump.SendAsync(() => Task.CompletedTask, TimeSpan.FromMilliseconds(0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _pump.SendAsync(() => Task.FromResult(true), TimeSpan.FromMilliseconds(-2)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _pump.SendAsync(() => Task.FromResult(true), TimeSpan.FromMilliseconds(0)));
    }
}
