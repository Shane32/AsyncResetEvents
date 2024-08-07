namespace AsyncResetEventTests;

public class AsyncMessagePumpTests
{
    [Fact]
    public void NullCallbackThrows()
    {
        Assert.ThrowsAny<ArgumentNullException>(() => new AsyncMessagePump<string>(null!));
    }

    [Fact]
    public void CallsSynchronously()
    {
        var sb = new StringBuilder();
        var pump = new AsyncMessagePump<string>(t => {
            Thread.Sleep(int.Parse(t));
            sb.Append(t + " ");
            return Task.CompletedTask;
        });
        pump.Post("100");
        pump.Post("1");
        Assert.Equal("100 1 ", sb.ToString());
    }

    [Fact]
    public async Task CallsAsynchronously()
    {
        var reset = new AsyncManualResetEvent();
        var sb = new StringBuilder();
        var pump = new AsyncMessagePump<string>(async t => {
            await Task.Delay(int.Parse(t));
            lock (sb)
                sb.Append(t + " ");
            if (t == "1")
                reset.Set();
        });
        pump.Post("100");
        pump.Post("1");
        await reset.WaitAsync();
        lock (sb)
            Assert.Equal("100 1 ", sb.ToString());
    }

    [Fact]
    public async Task CallsAsynchronouslyTasks()
    {
        var reset = new AsyncManualResetEvent();
        var sb = new StringBuilder();
        var pump = new AsyncMessagePump<string>(async t => {
            await Task.Delay(int.Parse(t));
            lock (sb)
                sb.Append(t + " ");
            if (t == "1")
                reset.Set();
        });
        var func = async () => {
            await Task.Delay(100);
            return "100";
        };
        pump.Post(func());
        pump.Post("1");
        await reset.WaitAsync();
        lock (sb)
            Assert.Equal("100 1 ", sb.ToString());
    }

    [Fact]
    public void HandlesErrorsGracefully()
    {
        var sb = new StringBuilder();
        var pump = new AsyncMessagePump<string>(t => {
            int.Parse(t);
            sb.Append(t + " ");
        });
        pump.Post("1");
        pump.Post("test");
        pump.Post("2");
        Assert.Equal("1 2 ", sb.ToString());
    }

    [Fact]
    public void HandlesErrorsGracefullyDerived()
    {
        var sb = new StringBuilder();
        var pump = new DerivedAsyncMessagePump(t => {
            int.Parse(t);
            sb.Append(t + " ");
        }, sb);
        pump.Post("1");
        pump.Post("test");
        pump.Post("2");
        Assert.Equal("1 FormatException 2 ", sb.ToString());
    }

    [Fact]
    public async Task CountWorks()
    {
        var tcs = new AsyncManualResetEvent[4];
        tcs[0] = new();
        tcs[1] = new();
        tcs[2] = new();
        tcs[3] = new();
        var pump = new AsyncMessagePump<int>(async i => {
            await tcs[i].WaitAsync().ConfigureAwait(false);
            tcs[i + 2].Set();
        });
        pump.Post(0);
        Assert.Equal(1, pump.Count);
        pump.Post(1);
        Assert.Equal(2, pump.Count);
        tcs[0].Set();
        Assert.True(await tcs[2].WaitAsync(30000, default).ConfigureAwait(false));
        tcs[1].Set();
        Assert.True(await tcs[3].WaitAsync(30000, default).ConfigureAwait(false));
        Assert.InRange(pump.Count, 0, 1); // probably 1, due to synchronous execution of tcs[i + 2].Set()
    }

    [Fact]
    public async Task DrainWorks_Pending()
    {
        var reset = new AsyncManualResetEvent();
        var done = false;
        var pending = true;
        var pump = new AsyncMessagePump<string>(async s => {
            pending = true;
            await reset.WaitAsync();
            done = true;
        });
        pump.Post("1");
        var task = pump.DrainAsync();
        Assert.False(task.IsCompleted);
        Assert.False(done);
        Assert.True(pending);
        reset.Set(true);
        await task;
        Assert.True(done);
    }

    [Fact]
    public async Task DrainWorks_Pending_2()
    {
        var reset = new AsyncManualResetEvent();
        var done = 0;
        var pending = 0;
        var pump = new AsyncMessagePump<string>(async s => {
            pending += 1;
            await reset.WaitAsync();
            done += 1;
        });

        pump.Post("1");
        var task1 = pump.DrainAsync();
        Assert.False(task1.IsCompleted);
        Assert.Equal(0, done);
        Assert.Equal(1, pending);

        pump.Post("2");
        var task2 = pump.DrainAsync();
        Assert.False(task2.IsCompleted);
        Assert.Equal(task1, task2);
        Assert.Equal(0, done);
        Assert.Equal(1, pending);

        reset.Set(true);
        await task1;
        Assert.Equal(2, done);
        await task2;
        Assert.Equal(2, done);
    }

    [Fact]
    public void DrainWorks_Synchronous()
    {
        var done = false;
        var pump = new AsyncMessagePump<string>(async s => done = true);
        pump.Post("1");
        Assert.True(done);
        var task = pump.DrainAsync();
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public async Task ExecutionContextCopied()
    {
        var al = new AsyncLocal<string?>();
        int success = 0;
        int expectedSuccess = 3;
        var tcs = new TaskCompletionSource<int>();
        var pump = new AsyncMessagePump<string?>(async str => {
            await Task.Delay(100);
            if (str == al.Value) {
                var incremented = Interlocked.Increment(ref success);
                if (incremented == expectedSuccess)
                    tcs.SetResult(incremented);
            }
        });

        al.Value = "1";
        pump.Post("1");
        al.Value = "2";
        pump.Post(Task.FromResult<string?>("2"));
        al.Value = "3";
        pump.Post("3");
        al.Value = null;

        await tcs.Task;
    }

#if NETCOREAPP2_1_OR_GREATER
    [Fact]
    public async Task ExecutionContextCopiedFromDefault()
    {
        Task t;
        // erase all execution context
        using (ExecutionContext.SuppressFlow()) {
            t = Task.Run(async () => {
                // now there is no execution context
                Assert.True(IsDefaultExecutionContext());

                var al = new AsyncLocal<string?>();
                int success = 0;
                int expectedSuccess = 4;
                var tcs = new TaskCompletionSource<int>();
                var pump = new AsyncMessagePump<string?>(async str => {
                    await Task.Delay(100);
                    if (str == null) {
                        if (IsDefaultExecutionContext()) {
                            var incremented = Interlocked.Increment(ref success);
                            if (incremented == expectedSuccess)
                                tcs.SetResult(incremented);
                        }
                    } else if (str == al.Value) {
                        var incremented = Interlocked.Increment(ref success);
                        if (incremented == expectedSuccess)
                            tcs.SetResult(incremented);
                    }
                });

                pump.Post((string?)null);
                Assert.True(IsDefaultExecutionContext());
                al.Value = "1";
                Assert.False(IsDefaultExecutionContext());
                pump.Post("1");
                al.Value = "2";
                pump.Post(Task.FromResult<string?>("2"));
                al.Value = "3";
                pump.Post("3");
                al.Value = null;

                await tcs.Task;
            });
        }
        await t;

        bool IsDefaultExecutionContext()
        {
            using var context = ExecutionContext.Capture();
            var isDefaultProperty = typeof(ExecutionContext).GetProperty("IsDefault", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            return (bool)isDefaultProperty.GetValue(context)!;
        }
    }
#endif

    public class DerivedAsyncMessagePump : AsyncMessagePump<string>
    {
        private readonly StringBuilder _sb;
        public DerivedAsyncMessagePump(Action<string> func, StringBuilder sb) : base(func)
        {
            _sb = sb;
        }

        protected override Task HandleErrorAsync(Exception exception)
        {
            _sb.Append(exception.GetType().Name + " ");
            throw new Exception("testing");
        }
    }
}
