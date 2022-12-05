using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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
            var task = are.WaitAsync(token);
            are.Set();
            await task;
        }
        var maxMem = GC.GetTotalMemory(true);
        Assert.InRange(maxMem, 0, mem * 2);
    }
}
