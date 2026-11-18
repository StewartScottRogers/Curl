using System.Text;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>
/// Checks that a <c>-d</c> body sent through the engine's handler wrapper is reported as the upload size and
/// that the transfer has a total time (BL-5410).
/// </summary>
[TestClass]
public sealed class RedisUploadSizeThroughEngineTests
{
    [TestMethod]
    public async Task ExecuteAsync_PostData_ReportsItsBytesAsTheUploadSizeAndATotalTime()
    {
        SteppingClock time = new();
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying("%1\r\n$5\r\nproto\r\n:3\r\n", "+OK\r\n");
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("redis://cache/SET/kk"),
            Output = new MemoryStream(),
            PostData = Encoding.UTF8.GetBytes("xyz"),
            TimeProvider = time,
        };
        IProtocolHandler handler = new EndPointReportingProtocolHandler(
            new RedisProtocolHandler(new ScriptedRedisConnector(ConnectResult.Connected(connection))),
            new ConnectionEndPointRecorder());

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.Report!.UploadSize);
        Assert.IsTrue(result.Report.Timings!.Completed > result.Report.Timings.Started);
    }

    // A clock that moves one tick on every read, so a transfer takes a non-zero time.
    private sealed class SteppingClock : TimeProvider
    {
        private long now;

        public override long GetTimestamp() => ++now;
    }
}
