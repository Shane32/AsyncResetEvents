namespace AsyncResetEventTests;

public class AutoResetEventTests
{
    [Fact]
    public async Task InvalidTimespanThrows()
    {
        AsyncAutoResetEvent reset = new();
        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => reset.WaitAsync(TimeSpan.FromSeconds(-2)));
    }

    [Fact]
    public void DoesNotRunUntilSignaled()
    {
        bool ran = false;
        AsyncAutoResetEvent reset = new();
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
    }

    [Fact]
    public void DoesNotRunUntilSignaled2()
    {
        bool ran = false;
        AsyncAutoResetEvent reset = new();
        _ = reset.WaitAsync(-1).ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
    }

    [Fact]
    public void DoesNotRunUntilSignaledMultipleTimes()
    {
        bool ran = false;
        AsyncAutoResetEvent reset = new();
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
        ran = false;
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
    }

    [Fact]
    public void CanBeSetBeforeWait()
    {
        bool ran = false;
        AsyncAutoResetEvent reset = new();
        reset.Set();
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.True(ran);
        ran = false;
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
        ran = false;
        reset.Set();
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.True(ran);
    }

    [Fact]
    public async void CanRunSetOnBackgroundThread()
    {
        bool ran = false;
        AsyncAutoResetEvent reset = new();
        var task = reset.WaitAsync().ContinueWith(_ => { Thread.Sleep(100); ran = true; }, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set(true);
        Assert.False(ran);
        await task;
    }

    [Fact]
    public async Task TimesOut()
    {
        AsyncAutoResetEvent reset = new();
        Assert.False(await reset.WaitAsync(100));

        var t = reset.WaitAsync();
        Assert.False(t.IsCompleted);
        reset.Set();
        await t;
    }

    [Fact]
    public async Task CancelsOut()
    {
        AsyncAutoResetEvent reset = new();
        CancellationTokenSource cts = new();
        cts.CancelAfter(100);
        try {
            await reset.WaitAsync(cts.Token);
            throw new Exception();
        } catch (OperationCanceledException) {
        }

        var t = reset.WaitAsync();
        Assert.False(t.IsCompleted);
        reset.Set();
        await t;
    }

    [Fact]
    public async Task CancelsOutWithTimer()
    {
        AsyncAutoResetEvent reset = new();
        CancellationTokenSource cts = new();
        cts.CancelAfter(100);
        try {
            await reset.WaitAsync(30000, cts.Token);
            throw new Exception();
        } catch (OperationCanceledException) {
        }

        var t = reset.WaitAsync();
        Assert.False(t.IsCompleted);
        reset.Set();
        await t;
    }

    [Fact]
    public async Task WorksWithDelay()
    {
        AsyncAutoResetEvent reset = new();
        var wait = reset.WaitAsync();
        await Task.Delay(100);
        Assert.False(wait.IsCompleted);
        reset.Set();
        Assert.True(wait.IsCompleted);
    }

    [Fact]
    public async Task WorksWithTimeout()
    {
        AsyncAutoResetEvent reset = new();
        var wait = reset.WaitAsync(30000);
        await Task.Delay(100);
        Assert.False(wait.IsCompleted);
        reset.Set();
        Assert.True(await wait);
    }

    [Fact]
    public async Task WorksZeroTimeout()
    {
        AsyncAutoResetEvent reset = new();
        Assert.False(await reset.WaitAsync(0));
        reset.Set();
        Assert.True(await reset.WaitAsync(0));
        Assert.False(await reset.WaitAsync(0));
    }
}
