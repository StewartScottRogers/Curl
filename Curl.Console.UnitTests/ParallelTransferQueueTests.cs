namespace Curl.Console;

/// <summary>
/// Pins the <c>-Z</c> queue: a slot is free while fewer than the maximum run, taken until a running
/// transfer ends, and the run waits for every transfer started.
/// </summary>
[TestClass]
public sealed class ParallelTransferQueueTests
{
    [TestMethod]
    public async Task WaitForFreeSlotAsync_BelowTheMaximum_CompletesAtOnce()
    {
        ParallelTransferQueue queue = new(2);
        queue.Add(new TaskCompletionSource().Task);

        Task slot = queue.WaitForFreeSlotAsync();

        Assert.IsTrue(slot.IsCompleted);
        await slot;
    }

    [TestMethod]
    public async Task WaitForFreeSlotAsync_AtTheMaximum_WaitsForATransferToEnd()
    {
        ParallelTransferQueue queue = new(2);
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(first.Task);
        queue.Add(second.Task);

        Task slot = queue.WaitForFreeSlotAsync();
        Assert.IsFalse(slot.IsCompleted);
        second.SetResult();
        await slot;

        Assert.IsFalse(first.Task.IsCompleted);
    }

    [TestMethod]
    public async Task WhenAllEndedAsync_WaitsForEveryTransferStarted()
    {
        ParallelTransferQueue queue = new(1);
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(Task.CompletedTask);
        await queue.WaitForFreeSlotAsync();
        queue.Add(first.Task);

        Task all = queue.WhenAllEndedAsync();
        Assert.IsFalse(all.IsCompleted);
        first.SetResult();

        await all;
    }
}
