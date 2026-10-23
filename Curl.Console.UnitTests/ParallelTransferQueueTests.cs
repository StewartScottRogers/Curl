using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-Z</c> queue: a slot is free while fewer than the maximum run, taken until a running
/// transfer ends, and the run waits for every transfer started.
/// </summary>
[TestClass]
public sealed class ParallelTransferQueueTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WaitForFreeSlotAsync_BelowTheMaximum_CompletesAtOnce()
    {
        Diagnostics.Arrange("queue", "maximum 2, one unfinished transfer added");
        ParallelTransferQueue queue = new(2);
        queue.Add(new TaskCompletionSource().Task);

        Task slot = queue.WaitForFreeSlotAsync();
        Diagnostics.Act("slot wait completed", slot.IsCompleted);

        Diagnostics.Assert("slot wait completed", true, slot.IsCompleted);
        Assert.IsTrue(slot.IsCompleted);
        await slot;
    }

    [TestMethod]
    public async Task WaitForFreeSlotAsync_AtTheMaximum_WaitsForATransferToEnd()
    {
        Diagnostics.Arrange("queue", "maximum 2, two unfinished transfers added; the second ends while waiting");
        ParallelTransferQueue queue = new(2);
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(first.Task);
        queue.Add(second.Task);

        Task slot = queue.WaitForFreeSlotAsync();
        Diagnostics.Act("slot wait completed at the maximum", slot.IsCompleted);
        Diagnostics.Assert("slot wait completed at the maximum", false, slot.IsCompleted);
        Assert.IsFalse(slot.IsCompleted);
        second.SetResult();
        await slot;

        Diagnostics.Assert("first transfer completed", false, first.Task.IsCompleted);
        Assert.IsFalse(first.Task.IsCompleted);
    }

    [TestMethod]
    public async Task WhenAllEndedAsync_WaitsForEveryTransferStarted()
    {
        Diagnostics.Arrange("queue", "maximum 1, one ended transfer, then one unfinished transfer");
        ParallelTransferQueue queue = new(1);
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(Task.CompletedTask);
        await queue.WaitForFreeSlotAsync();
        queue.Add(first.Task);

        Task all = queue.WhenAllEndedAsync();
        Diagnostics.Act("all ended while one runs", all.IsCompleted);
        Diagnostics.Assert("all ended while one runs", false, all.IsCompleted);
        Assert.IsFalse(all.IsCompleted);
        first.SetResult();

        await all;
    }

    [TestMethod]
    public async Task WaitForFreeSlotAsync_SlotFrees_StartsTheEarliestWaitingTransferBeforeTheHostQueueFreedOnes()
    {
        Diagnostics.Arrange("queue", "maximum 1, one unfinished transfer; the next waits for its slot, the host queue's freed ones wait for BeforeEachWait");
        ParallelTransferQueue queue = new(1);
        List<string> events = [];
        queue.BeforeEachWait = () => events.Add("host queue starts its freed transfers");
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(first.Task);

        Task slot = queue.WaitForFreeSlotAsync();
        first.SetResult();
        await slot;
        events.Add("slot's waiting transfer starts");
        queue.Add(Task.CompletedTask);
        await queue.WhenAllEndedAsync();
        Diagnostics.Act("events", string.Join(" | ", events));

        string expected = "host queue starts its freed transfers | slot's waiting transfer starts | host queue starts its freed transfers";
        Diagnostics.Assert("events", expected, string.Join(" | ", events));
        Assert.AreEqual(expected, string.Join(" | ", events));
    }
}