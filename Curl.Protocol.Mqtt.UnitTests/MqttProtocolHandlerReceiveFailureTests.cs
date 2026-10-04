using System.Net.Sockets;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Pins how an <c>mqtt://</c> transfer whose read fails ends, as curl 8.21.0 does (BL-1341):
/// exit 56 with the socket filter's <c>Recv failure: &lt;words&gt;</c> for a socket error, the
/// words <c>CurlSocketErrorText</c>'s for the platform, and <c>curl_easy_strerror</c>'s
/// <c>Failure when receiving data from the peer</c> for a read that fails any other way or a
/// connection closed mid-packet.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerReceiveFailureTests
{
    private const string ReceiveFailed = "Failure when receiving data from the peer";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ConnackReadAborted_IsWinsockRecvFailure()
    {
        Run run = await RunAsync(new ScriptedConnection([null]) { ReadFailure = Failing(SocketError.ConnectionAborted) });

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, "Recv failure: Connection was aborted"), run.Result);
        CollectionAssert.Contains(run.Transcript, "* Recv failure: Connection was aborted");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ConnackReadAborted_IsStrerrorRecvFailure()
    {
        IOException failure = Failing(SocketError.ConnectionAborted);

        Run run = await RunAsync(new ScriptedConnection([null]) { ReadFailure = failure });

        string expected = "Recv failure: " + failure.InnerException!.Message;
        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, expected), run.Result);
        CollectionAssert.Contains(run.Transcript, "* " + expected);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackReadFailsWithoutSocketError_IsFallbackTextWithNoLine()
    {
        Run run = await RunAsync(new ScriptedConnection([null]) { ReadFailure = new IOException("The read failed.") });

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, ReceiveFailed), run.Result);
        CollectionAssert.DoesNotContain(run.Transcript, "* " + ReceiveFailed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedMidConnack_IsFallbackText()
    {
        Run run = await RunAsync(new ScriptedConnection([0x20, 0x02, 0x00]));

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, ReceiveFailed), run.Result);
    }

    private static IOException Failing(SocketError error) =>
        new("The read failed.", new SocketException((int)error));

    private static async Task<Run> RunAsync(ScriptedConnection connection)
    {
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt://h/t"),
            Output = new RecordingStream(),
            Events = events,
        };

        TransferResult result = await new MqttProtocolHandler(FakeConnector.For(connection), () => "PBadK4E3")
            .ExecuteAsync(context);
        return new Run(result, events.Transcript);
    }

    private sealed record Run(TransferResult Result, List<string> Transcript);
}
