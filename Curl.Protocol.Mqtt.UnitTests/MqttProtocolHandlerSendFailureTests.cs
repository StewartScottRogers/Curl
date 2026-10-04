using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Pins how an <c>mqtt://</c> transfer whose packet cannot be sent ends, as curl 8.21.0 does
/// (BL-1229, BL-1341): exit 55 with the socket filter's <c>Send failure: &lt;words&gt;</c> for
/// a socket error, the words <c>CurlSocketErrorText</c>'s for the platform, and
/// <c>curl_easy_strerror</c>'s <c>Failed sending data to the peer</c> otherwise, and, for the
/// CONNECT only, <c>mqtt_do</c>'s <c>Error 55 sending MQTT CONNECT request</c> after the
/// message in <c>-v</c>.
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
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ConnectReset_IsWinsockSendFailureThenConnectNotSent()
    {
        Run run = await RunAsync(Failing(SocketError.ConnectionReset), writesBeforeFailure: 0, postData: null);

        AssertSendFailure(run, "Send failure: Connection was reset", ConnectNotSent);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ConnectReset_IsStrerrorSendFailureThenConnectNotSent()
    {
        IOException failure = Failing(SocketError.ConnectionReset);

        Run run = await RunAsync(failure, writesBeforeFailure: 0, postData: null);

        AssertSendFailure(run, "Send failure: " + failure.InnerException!.Message, ConnectNotSent);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ConnectAborted_IsWinsockSendFailureThenConnectNotSent()
    {
        Run run = await RunAsync(Failing(SocketError.ConnectionAborted), writesBeforeFailure: 0, postData: null);

        AssertSendFailure(run, "Send failure: Connection was aborted", ConnectNotSent);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ConnectAborted_IsStrerrorSendFailureThenConnectNotSent()
    {
        IOException failure = Failing(SocketError.ConnectionAborted);

        Run run = await RunAsync(failure, writesBeforeFailure: 0, postData: null);

        AssertSendFailure(run, "Send failure: " + failure.InnerException!.Message, ConnectNotSent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFailsWithoutSocketError_IsFallbackTextWithOnlyConnectNotSent()
    {
        Run run = await RunAsync(WithoutSocketError(), writesBeforeFailure: 0, postData: null);

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Failed sending data to the peer"), run.Result);
        Assert.IsTrue(run.Transcript[^3].StartsWith("> ", StringComparison.Ordinal));
        CollectionAssert.AreEqual(new[] { ConnectNotSent, ShuttingDown }, run.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_SubscribeReset_IsWinsockSendFailureWithoutConnectNotSent()
    {
        Run run = await RunAsync(Failing(SocketError.ConnectionReset), writesBeforeFailure: 1, postData: null);

        AssertSendFailure(run, "Send failure: Connection was reset", Sent(Subscribe));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_SubscribeReset_IsStrerrorSendFailureWithoutConnectNotSent()
    {
        IOException failure = Failing(SocketError.ConnectionReset);

        Run run = await RunAsync(failure, writesBeforeFailure: 1, postData: null);

        AssertSendFailure(run, "Send failure: " + failure.InnerException!.Message, Sent(Subscribe));
    }

    [TestMethod]
    public async Task ExecuteAsync_SubscribeFailsWithoutSocketError_IsFallbackTextWithNoLine()
    {
        Run run = await RunAsync(WithoutSocketError(), writesBeforeFailure: 1, postData: null);

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Failed sending data to the peer"), run.Result);
        CollectionAssert.AreEqual(new[] { Sent(Subscribe), ShuttingDown }, run.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_PublishReset_IsWinsockSendFailureWithoutConnectNotSent()
    {
        Run run = await RunAsync(Failing(SocketError.ConnectionReset), writesBeforeFailure: 1, postData: "x"u8.ToArray());

        AssertSendFailure(run, "Send failure: Connection was reset", Sent(Publish));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_PublishReset_IsStrerrorSendFailureWithoutConnectNotSent()
    {
        IOException failure = Failing(SocketError.ConnectionReset);

        Run run = await RunAsync(failure, writesBeforeFailure: 1, postData: "x"u8.ToArray());

        AssertSendFailure(run, "Send failure: " + failure.InnerException!.Message, Sent(Publish));
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishFailsWithoutSocketError_IsFallbackTextWithNoLine()
    {
        Run run = await RunAsync(WithoutSocketError(), writesBeforeFailure: 1, postData: "x"u8.ToArray());

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Failed sending data to the peer"), run.Result);
        CollectionAssert.AreEqual(new[] { Sent(Publish), ShuttingDown }, run.Transcript.TakeLast(2).ToArray());
    }

    /// <summary>
    /// Asserts exit 55 with <paramref name="message" />, written in <c>-v</c> with
    /// <paramref name="neighbour" />: the line after it for the CONNECT, the packet sent
    /// before it otherwise.
    /// </summary>
    private static void AssertSendFailure(Run run, string message, string neighbour)
    {
        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, message), run.Result);
        string[] expected = neighbour == ConnectNotSent
            ? ["* " + message, neighbour, ShuttingDown]
            : [neighbour, "* " + message, ShuttingDown];
        CollectionAssert.AreEqual(expected, run.Transcript.TakeLast(3).ToArray());
    }

    private static IOException Failing(SocketError error) =>
        new("The write failed.", new SocketException((int)error));

    private static IOException WithoutSocketError() => new("The write failed.");

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
