using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the run-wide write gate: one holder at a time, a waiting writer let in when the holder lets
/// go, and a flow that holds the gate let straight through, asynchronously and synchronously.
/// </summary>
[TestClass]
public sealed class WriteGateTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunExclusiveAsync_WhileAnotherFlowHoldsTheGate_WaitsForIt()
    {
        WriteGate gate = new();
        TaskCompletionSource letGo = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> order = [];
        Diagnostics.Arrange("holder", "holds the gate until let go");
        Task holder = gate.RunExclusiveAsync(async () =>
        {
            order.Add("holder in");
            await letGo.Task;
            order.Add("holder out");
        });

        Task waiter = gate.RunExclusiveAsync(() =>
        {
            order.Add("waiter");
            return Task.CompletedTask;
        });
        Diagnostics.Act("waiter completed before the holder lets go", waiter.IsCompleted);
        Assert.IsFalse(waiter.IsCompleted);
        letGo.SetResult();
        await Task.WhenAll(holder, waiter);
        Diagnostics.Act("order", string.Join(", ", order));

        Diagnostics.Assert("order", "holder in, holder out, waiter", string.Join(", ", order));
        CollectionAssert.AreEqual(new[] { "holder in", "holder out", "waiter" }, order);
    }

    [TestMethod]
    public async Task RunExclusiveAsync_InsideTheGate_RunsAtOnce()
    {
        WriteGate gate = new();
        bool ranInside = false;
        Diagnostics.Arrange("write", "RunExclusiveAsync inside RunExclusiveAsync");

        await gate.RunExclusiveAsync(() => gate.RunExclusiveAsync(() =>
        {
            ranInside = true;
            return Task.CompletedTask;
        }));
        Diagnostics.Act("ran inside", ranInside);

        Diagnostics.Assert("ran inside", true, ranInside);
        Assert.IsTrue(ranInside);
    }

    [TestMethod]
    public async Task RunExclusive_InsideTheGate_RunsAtOnce()
    {
        WriteGate gate = new();
        bool ranInside = false;
        Diagnostics.Arrange("write", "RunExclusive inside RunExclusiveAsync");

        await gate.RunExclusiveAsync(() =>
        {
            gate.RunExclusive(() => ranInside = true);
            return Task.CompletedTask;
        });
        Diagnostics.Act("ran inside", ranInside);

        Diagnostics.Assert("ran inside", true, ranInside);
        Assert.IsTrue(ranInside);
    }

    [TestMethod]
    public async Task RunExclusive_OutsideTheGate_TakesItAndLetsItGo()
    {
        WriteGate gate = new();
        bool ran = false;
        Diagnostics.Arrange("writes", "RunExclusive, then RunExclusiveAsync");

        gate.RunExclusive(() => ran = true);
        Task after = gate.RunExclusiveAsync(() => Task.CompletedTask);
        Diagnostics.Act("ran / next write completed", $"{ran} / {after.IsCompleted}");

        Diagnostics.Assert("ran / next write completed", "True / True", $"{ran} / {after.IsCompleted}");
        Assert.IsTrue(ran);
        Assert.IsTrue(after.IsCompleted);
        await after;
    }

    [TestMethod]
    public async Task RunExclusiveAsync_WriteThrows_LetsTheGateGo()
    {
        WriteGate gate = new();
        Diagnostics.Arrange("writes", "an asynchronous and a synchronous write that throw IOException");

        await Assert.ThrowsExactlyAsync<IOException>(() => gate.RunExclusiveAsync(() => throw new IOException()));
        Assert.ThrowsExactly<IOException>(() => gate.RunExclusive(() => throw new IOException()));
        Diagnostics.Act("both writes threw", nameof(IOException));

        Diagnostics.Assert("next write completed at once", true, gate.RunExclusiveAsync(() => Task.CompletedTask).IsCompleted);
        Assert.IsTrue(gate.RunExclusiveAsync(() => Task.CompletedTask).IsCompleted);
    }
}
