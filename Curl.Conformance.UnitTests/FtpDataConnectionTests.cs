using System.Text;

namespace Curl.Conformance;

/// <summary>Pins <see cref="FtpDataConnection.SendSlowlyAsync"/>, ftpserver.pl's <c>SLOWDOWNDATA</c> pacing.</summary>
[TestClass]
public sealed class FtpDataConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendSlowlyAsync_ClientDisposed_StopsSendingAndLeavesTheConnectionOpen()
    {
        ManualTimeProvider clock = new();
        FtpDataConnection connection = new();
        Task sending = connection.SendSlowlyAsync(Encoding.Latin1.GetBytes("abc"), clock);
        byte[] buffer = new byte[8];
        int first = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        await connection.DisposeAsync();
        clock.Advance(FtpDataConnection.SlowDataByteDelay);
        await sending;
        Task<int> next = connection.ReadAsync(buffer, TestContext.CancellationToken).AsTask();

        Assert.AreEqual(1, first);
        Assert.IsFalse(next.IsCompleted);
    }
}
