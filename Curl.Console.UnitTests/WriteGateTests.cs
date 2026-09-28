namespace Curl.Console;

/// <summary>
/// Pins the run-wide write gate: one holder at a time, a waiting writer let in when the holder lets
/// go, and a flow that holds the gate let straight through, asynchronously and synchronously.
/// </summary>
[TestClass]
public sealed class WriteGateTests
{
    [TestMethod]
    public async Task RunExclusiveAsync_WhileAnotherFlowHoldsTheGate_WaitsForIt()
    {
        WriteGate gate = new();
        TaskCompletionSource letGo = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> order = [];
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
        Assert.IsFalse(waiter.IsCompleted);
        letGo.SetResult();
        await Task.WhenAll(holder, waiter);

        CollectionAssert.AreEqual(new[] { "holder in", "holder out", "waiter" }, order);
    }

    [TestMethod]
    public async Task RunExclusiveAsync_InsideTheGate_RunsAtOnce()
    {
        WriteGate gate = new();
        bool ranInside = false;

        await gate.RunExclusiveAsync(() => gate.RunExclusiveAsync(() =>
        {
            ranInside = true;
            return Task.CompletedTask;
        }));

        Assert.IsTrue(ranInside);
    }

    [TestMethod]
    public async Task RunExclusive_InsideTheGate_RunsAtOnce()
    {
        WriteGate gate = new();
        bool ranInside = false;

        await gate.RunExclusiveAsync(() =>
        {
            gate.RunExclusive(() => ranInside = true);
            return Task.CompletedTask;
        });

        Assert.IsTrue(ranInside);
    }

    [TestMethod]
    public async Task RunExclusive_OutsideTheGate_TakesItAndLetsItGo()
    {
        WriteGate gate = new();
        bool ran = false;

        gate.RunExclusive(() => ran = true);
        Task after = gate.RunExclusiveAsync(() => Task.CompletedTask);

        Assert.IsTrue(ran);
        Assert.IsTrue(after.IsCompleted);
        await after;
    }

    [TestMethod]
    public async Task RunExclusiveAsync_WriteThrows_LetsTheGateGo()
    {
        WriteGate gate = new();

        await Assert.ThrowsExactlyAsync<IOException>(() => gate.RunExclusiveAsync(() => throw new IOException()));
        Assert.ThrowsExactly<IOException>(() => gate.RunExclusive(() => throw new IOException()));

        Assert.IsTrue(gate.RunExclusiveAsync(() => Task.CompletedTask).IsCompleted);
    }
}
