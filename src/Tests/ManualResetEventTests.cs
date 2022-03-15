namespace AsyncResetEventTests;

public class ManualResetEventTests
{
    [Fact]
    public void DoesNotRunUntilSignaled()
    {
        bool ran = false;
        AsyncManualResetEvent reset = new();
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
    }

    [Fact]
    public async void CanRunSetOnBackgroundThread()
    {
        bool ran = false;
        AsyncManualResetEvent reset = new();
        var task = reset.WaitAsync().ContinueWith(_ => { Thread.Sleep(100); ran = true; }, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set(true);
        Assert.False(ran);
        await task;
    }

    [Fact]
    public void CanWaitMultipleTimes()
    {
        bool ran = false;
        AsyncManualResetEvent reset = new();
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
        ran = false;
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.True(ran);
    }

    [Fact]
    public void CanBeReset()
    {
        bool ran = false;
        AsyncManualResetEvent reset = new();
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
        reset.Reset();
        ran = false;
        _ = reset.WaitAsync().ContinueWith(_ => ran = true, TaskContinuationOptions.ExecuteSynchronously);
        Assert.False(ran);
        reset.Set();
        Assert.True(ran);
    }

    [Fact]
    public async Task StartsAsSpecified()
    {
        AsyncManualResetEvent reset = new();
        Assert.False(await reset.WaitAsync(0));
        reset = new(false);
        Assert.False(await reset.WaitAsync(0));
        reset = new(true);
        Assert.True(await reset.WaitAsync(0));
    }

    [Fact]
    public async Task WorksWithDelay()
    {
        AsyncManualResetEvent reset = new();
        var wait = reset.WaitAsync();
        await Task.Delay(100);
        Assert.False(wait.IsCompleted);
        reset.Set();
        Assert.True(wait.IsCompleted);
    }

    [Fact]
    public async Task WorksWithTimeout()
    {
        AsyncManualResetEvent reset = new();
        var wait = reset.WaitAsync(30000);
        await Task.Delay(100);
        Assert.False(wait.IsCompleted);
        reset.Set();
        Assert.True(await wait);
    }

    [Fact]
    public async Task TimesOut()
    {
        AsyncManualResetEvent reset = new();
        Assert.False(await reset.WaitAsync(100));
    }

    [Fact]
    public async Task CancelsOut()
    {
        AsyncManualResetEvent reset = new();
        CancellationTokenSource cts = new();
        cts.CancelAfter(100);
        try {
            await reset.WaitAsync(cts.Token);
            throw new Exception();
        }
        catch (OperationCanceledException) {
            return;
        }
    }

    [Fact]
    public async Task CancelsOutWithTimer()
    {
        AsyncManualResetEvent reset = new();
        CancellationTokenSource cts = new();
        cts.CancelAfter(100);
        try {
            await reset.WaitAsync(30000, cts.Token);
            throw new Exception();
        } catch (OperationCanceledException) {
            return;
        }
    }

    [Fact]
    public async Task WorksZeroTimeout()
    {
        AsyncManualResetEvent reset = new();
        Assert.False(await reset.WaitAsync(0));
        reset.Set();
        Assert.True(await reset.WaitAsync(0));
        Assert.True(await reset.WaitAsync(0));
    }
}
