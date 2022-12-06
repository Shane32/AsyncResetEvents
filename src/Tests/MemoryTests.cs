namespace Tests;

public class MemoryTests
{
    [Fact]
    public async Task AutoResetEvents_Wait_Token_Leak()
    {
        var are = new AsyncAutoResetEvent();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var mem = GC.GetTotalMemory(true);
        for (int i = 0; i < 200000; i++) {
            var task = TimeoutAfter((Task<bool>)are.WaitAsync(default), Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            //var task = Test((Task<bool>)are.WaitAsync(default), token).ConfigureAwait(false);
            are.Set();
            await task;
        }
        var maxMem = GC.GetTotalMemory(true);
        Assert.InRange(maxMem, 0, mem * 2);

        /*
        static async Task<bool> Test(Task<bool> t, CancellationToken token)
        {
            try {
                return await t.WaitAsync(token).ConfigureAwait(false);
            } catch (TimeoutException) {
                return false;
            }
        }
        */
        
        static async Task<bool> TimeoutAfter(Task<bool> task, TimeSpan timeout, CancellationToken cancellationToken)
        {

            using (var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {

                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token)).ConfigureAwait(false);
                if (completedTask == task) {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                } else {
                    timeoutCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    return false;
                }
            }
        }
    }
}
