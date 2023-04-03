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
