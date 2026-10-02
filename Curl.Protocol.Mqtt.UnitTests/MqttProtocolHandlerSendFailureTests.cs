using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Pins how an <c>mqtt://</c> transfer whose packet cannot be sent ends, as curl 8.21.0 does
/// (BL-1229): exit 55 with the socket filter's <c>Send failure: Connection was reset</c> for a
/// reset and <c>curl_easy_strerror</c>'s <c>Failed sending data to the peer</c> otherwise, and,
/// for the CONNECT only, <c>mqtt_do</c>'s <c>Error 55 sending MQTT CONNECT request</c> after
/// the message in <c>-v</c>.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerSendFailureTests
{
    private const string ShuttingDown = "* shutting down connection #0";

    private const string ConnectNotSent = "* Error 55 sending MQTT CONNECT request";

    private static readonly byte[] Connack = [0x20, 0x02, 0x00, 0x00];

    private static readonly byte[] Subscribe = [0x82, 0x06, 0x00, 0x01, 0x00, 0x01, 0x74, 0x00];

    private static readonly byte[] Publish = [0x30, 0x04, 0x00, 0x01, 0x74, 0x78];

    [TestMethod]
    public async Task ExecuteAsync_ConnectReset_IsSendFailureThenConnectNotSent()
    {
        Run run = await RunAsync(Reset(), writesBeforeFailure: 0, postData: null);

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Send failure: Connection was reset"), run.Result);
        CollectionAssert.AreEqual(
            new[] { "* Send failure: Connection was reset", ConnectNotSent, ShuttingDown },
            run.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_IsFallbackTextWithOnlyConnectNotSent()
    {
        Run run = await RunAsync(Other(), writesBeforeFailure: 0, postData: null);

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Failed sending data to the peer"), run.Result);
        Assert.IsTrue(run.Transcript[^3].StartsWith("> ", StringComparison.Ordinal));
        CollectionAssert.AreEqual(new[] { ConnectNotSent, ShuttingDown }, run.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SubscribeReset_IsSendFailureWithoutConnectNotSent()
    {
        Run run = await RunAsync(Reset(), writesBeforeFailure: 1, postData: null);

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Send failure: Connection was reset"), run.Result);
        CollectionAssert.AreEqual(
            new[] { Sent(Subscribe), "* Send failure: Connection was reset", ShuttingDown },
            run.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SubscribeFails_IsFallbackTextWithNoLine()
    {
        Run run = await RunAsync(Other(), writesBeforeFailure: 1, postData: null);

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Failed sending data to the peer"), run.Result);
        CollectionAssert.AreEqual(new[] { Sent(Subscribe), ShuttingDown }, run.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishReset_IsSendFailureWithoutConnectNotSent()
    {
        Run run = await RunAsync(Reset(), writesBeforeFailure: 1, postData: "x"u8.ToArray());

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Send failure: Connection was reset"), run.Result);
        CollectionAssert.AreEqual(
            new[] { Sent(Publish), "* Send failure: Connection was reset", ShuttingDown },
            run.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishFails_IsFallbackTextWithNoLine()
    {
        Run run = await RunAsync(Other(), writesBeforeFailure: 1, postData: "x"u8.ToArray());

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Failed sending data to the peer"), run.Result);
        CollectionAssert.AreEqual(new[] { Sent(Publish), ShuttingDown }, run.Transcript.TakeLast(2).ToArray());
    }

    private static IOException Reset() =>
        new("The peer reset the connection.", new SocketException((int)SocketError.ConnectionReset));

    private static IOException Other() =>
        new("The write failed.", new SocketException((int)SocketError.NetworkDown));

    private static string Sent(byte[] packet) => "> " + Encoding.Latin1.GetString(packet);

    private static async Task<Run> RunAsync(IOException failure, int writesBeforeFailure, byte[]? postData)
    {
        ScriptedConnection connection = new(Connack) { WriteFailure = failure, WritesBeforeFailure = writesBeforeFailure };
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt://h/t"),
            Output = new RecordingStream(),
            PostData = postData is null ? (ReadOnlyMemory<byte>?)null : postData,
            Events = events,
        };

        TransferResult result = await new MqttProtocolHandler(FakeConnector.For(connection), () => "PBadK4E3")
            .ExecuteAsync(context);
        return new Run(result, events.Transcript);
    }

    private sealed record Run(TransferResult Result, List<string> Transcript);
}
