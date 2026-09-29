using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Pins what an MQTT transfer writes to Curl's own diagnostic log, component <c>mqtt</c>
/// (ADR-0222, BL-928): the failure that ends it as <c>error</c>, each packet passed over as
/// <c>warning</c>, the request line, CONNACK return code, SUBSCRIBE or PUBLISH done and
/// transfer end as <c>info</c>, each packet's type and remaining length as <c>verbose</c>,
/// and never the CONNECT's password.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerDiagnosticLogTests
{
    private const string Secret = "s3cret";

    private static readonly byte[] Connack = Hex("20 02 00 00");

    private static readonly byte[] Suback = Hex("90 03 00 01 00");

    private static readonly byte[] Disconnect = Hex("E0 00");

    /// <summary>A PUBLISH to topic <c>t</c> with payload <c>hi</c>: five bytes after the fixed header.</summary>
    private static readonly byte[] PublishHi = [0x30, 0x05, 0x00, 0x01, .. "thi"u8];

    [TestMethod]
    public async Task ExecuteAsync_SubscribeAtInfo_LogsConnackSubscribeAndTransferEnd()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        TransferResult result = await RunAsync(new ScriptedConnection(Connack, Suback, PublishHi, Disconnect), log);

        Assert.AreEqual(TransferResult.Success(5), result);
        CollectionAssert.AreEqual(
            new[] { "CONNACK return code 0", "SUBSCRIBE t", "SUBSCRIBE done: SUBACK granted QoS 0", "transfer finished: 5 bytes in 250 ms" },
            log.At(DiagnosticLogLevel.Info));
        Assert.HasCount(4, log.Lines);
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Mqtt));
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishAtInfo_LogsThePublishLineAndItsEnd()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        TransferResult result = await RunAsync(new ScriptedConnection(Connack), log, "mqtt://h/a%2Fb", "hello"u8.ToArray());

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(
            new[] { "CONNACK return code 0", "PUBLISH a/b, 5 bytes", "PUBLISH done; DISCONNECT sent", "transfer finished: 0 bytes in 250 ms" },
            log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_SubscribeAtVerbose_LogsEachPacketTypeAndRemainingLength()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync(new ScriptedConnection(Connack, Suback, PublishHi, Disconnect), log);

        CollectionAssert.AreEqual(
            new[]
            {
                "sent CONNECT, remaining length 24",
                "received CONNACK, remaining length 2",
                "sent SUBSCRIBE, remaining length 6",
                "received SUBACK, remaining length 3",
                "received PUBLISH, remaining length 5",
                "received DISCONNECT, remaining length 0",
            },
            log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishOverOneLengthByte_LogsTheWholeRemainingLength()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync(new ScriptedConnection(Connack), log, "mqtt://h/t", new byte[200]);

        CollectionAssert.AreEqual(
            new[]
            {
                "sent CONNECT, remaining length 24",
                "received CONNACK, remaining length 2",
                "sent PUBLISH, remaining length 203",
                "sent DISCONNECT, remaining length 0",
            },
            log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackRefused_LogsTheReturnCodeAndAnErrorNamingTheExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        TransferResult result = await RunAsync(new ScriptedConnection(Hex("20 02 00 05")), log);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "CONNACK return code 5" }, log.At(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(
            new[] { "failed with WeirdServerReply (8): Expected 0000 but got 0005" },
            log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_LogsAnErrorNamingTheExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = new FakeConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));

        await new MqttProtocolHandler(connector, () => "PBadK4E3").ExecuteAsync(Context("mqtt://h/t", log));

        CollectionAssert.AreEqual(new[] { "failed with CouldntConnect (7): Failed to connect" }, log.At(DiagnosticLogLevel.Error));
        Assert.HasCount(1, log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtError_RecordsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        TransferResult result = await RunAsync(new ScriptedConnection(Connack, Suback, PublishHi), log);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.HasCount(1, log.Lines);
        Assert.AreEqual((DiagnosticLogLevel.Error, DiagnosticLogComponents.Mqtt, "failed with RecvError (56): Connection disconnected"), log.Lines[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Always_PassesTheDiagnosticLogToTheConnectTarget()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        FakeConnector connector = FakeConnector.For(new ScriptedConnection(Connack, Suback, Disconnect));

        await new MqttProtocolHandler(connector, () => "PBadK4E3").ExecuteAsync(Context("mqtt://h/t", log));

        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsAtVerbose_NeverLogsThePassword()
    {
        // curl mqtt://user:s3cret@host/topic
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("mqtt://user:" + Secret + "@host/topic"),
            Output = new RecordingStream(),
            DiagnosticLog = log,
            Credentials = new NetworkCredential("user", Secret),
        };

        TransferResult result = await new MqttProtocolHandler(FakeConnector.For(new ScriptedConnection(Connack, Suback, Disconnect)), () => "PBadK4E3")
            .ExecuteAsync(context);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotEmpty(log.Lines);
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "sent CONNECT, remaining length 38");
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(Secret, StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstPacketNotAConnack_WarnsItIsTakenAsTheConnack()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        await RunAsync(new ScriptedConnection(Hex("30 02 00 00"), Suback, Disconnect), log);

        CollectionAssert.AreEqual(
            new[] { "unexpected PUBLISH, remaining length 2: taken as the CONNACK" },
            log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPacketThenBodies_WarnsOfEachPacketPassedOver()
    {
        // An empty PUBACK drops the awaited CONNACK; the next body is left unread, and its
        // bytes "41 42" read as a PUBACK of 66 bytes end the transfer with exit 0.
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        TransferResult result = await RunAsync(new ScriptedConnection(Hex("40 00"), Hex("20 02 41 42")), log);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(
            new[]
            {
                "unexpected PUBACK, remaining length 0: ignored; the next packet's body is left unread",
                "unexpected CONNACK, remaining length 2: body left unread",
                "unexpected PUBACK, remaining length 66: not handled; the transfer ends",
            },
            log.At(DiagnosticLogLevel.Warning));
    }

    private static byte[] Hex(string text) => Convert.FromHexString(text.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static TransferContext Context(string url, IDiagnosticLog log, ReadOnlyMemory<byte>? postData = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new RecordingStream(),
        DiagnosticLog = log,
        PostData = postData,
        TimeProvider = new SteppingTimeProvider(),
    };

    private static async Task<TransferResult> RunAsync(
        ScriptedConnection connection,
        IDiagnosticLog log,
        string url = "mqtt://h/t",
        ReadOnlyMemory<byte>? postData = null) =>
        await new MqttProtocolHandler(FakeConnector.For(connection), () => "PBadK4E3")
            .ExecuteAsync(Context(url, log, postData));

    /// <summary>A clock that moves 250 ms each time it is read, so the elapsed time logged is fixed.</summary>
    private sealed class SteppingTimeProvider : TimeProvider
    {
        private long now;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => now += 250;
    }
}
