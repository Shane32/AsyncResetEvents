namespace Tests;

public class MemoryTests
{
    [Fact(Skip = "Doesn't work in CI")]
    public async Task AutoResetEvents_Wait_Token_Leak()
    {
        var are = new AsyncAutoResetEvent();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var mem = GC.GetTotalMemory(true);
        for (int i = 0; i < 200000; i++) {
            var task = are.WaitAsync(token).ConfigureAwait(false);
            are.Set();
            await task;
        }
        await Task.Yield();
        var maxMem = GC.GetTotalMemory(true);
        Assert.InRange(maxMem, 0, mem * 2);
    }

    [Fact(Skip = "Doesn't work in CI")]
    public async Task AutoResetEvents_Wait_Time_Leak()
    {
        var are = new AsyncAutoResetEvent();
        var mem = GC.GetTotalMemory(true);
        for (int i = 0; i < 200000; i++) {
            var task = are.WaitAsync(5000).ConfigureAwait(false);
            are.Set();
            await task;
        }
        await Task.Yield();
        var maxMem = GC.GetTotalMemory(true);
        Assert.InRange(maxMem, 0, mem * 2);
    }

    [Fact(Skip = "Doesn't work in CI")]
    public async Task AutoResetEvents_Wait_Time_Token_Leak()
    {
        var are = new AsyncAutoResetEvent();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var mem = GC.GetTotalMemory(true);
        for (int i = 0; i < 200000; i++) {
            var task = are.WaitAsync(5000, token).ConfigureAwait(false);
            are.Set();
            await task;
        }
        await Task.Yield();
        var maxMem = GC.GetTotalMemory(true);
        Assert.InRange(maxMem, 0, mem * 2);
    }

    [Fact(Skip = "Doesn't work in CI")]
    public async Task AutoResetEvents_Wait_Leak()
    {
        var are = new AsyncAutoResetEvent();
        var mem = GC.GetTotalMemory(true);
        for (int i = 0; i < 200000; i++) {
            var task = are.WaitAsync().ConfigureAwait(false);
            are.Set();
            await task;
        }
        await Task.Yield();
        var maxMem = GC.GetTotalMemory(true);
        Assert.InRange(maxMem, 0, mem * 2);
    }
}
