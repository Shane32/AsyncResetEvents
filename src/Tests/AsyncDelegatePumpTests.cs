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
        _pump.Post(async () => {
            await Task.Delay(100);
            WriteLine("100");
        });
        _pump.Post(async () => {
            Verify("100 ");
            await Task.Delay(1);
            WriteLine("1");
            _reset.Set();
        });
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
}
