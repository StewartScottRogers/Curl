using System.Net;
using Curl.Extension.Protocol.Amqp0.Methods;
using Curl.Extension.Protocol.Amqp0.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Amqp0.Client;

[TestClass]
public sealed class Amqp0ProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_NoBody_NegotiatesDeclaresGetsAndCloses()
    {
        byte[] body = [.. Enumerable.Range(0, 6000).Select(i => (byte)i)];
        byte[] server =
        [
            .. Handshake(channelMax: 100, frameMax: 0),
            .. Frame(new Amqp0BasicGetOk(1, false, string.Empty, "q", 0), 1),
            .. Amqp0Frame.Heartbeat.ToBytes(),
            .. Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, body, 0).SelectMany(frame => frame.ToBytes()),
            .. Closes(),
        ];
        ScriptedAmqp0Connection connection = new(server.Chunk(4000).ToArray());
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "amqp://broker//q", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(6000L, result.BytesTransferred);
        CollectionAssert.AreEqual(body, output.ToArray());
        Assert.HasCount(9, connection.Writes);
        CollectionAssert.AreEqual("AMQP\0\0\u0009\u0001"u8.ToArray(), connection.Writes[0]);
        var startOk = (Amqp0ConnectionStartOk)Method(connection.Writes[1]);
        Assert.AreEqual("PLAIN", startOk.Mechanism);
        CollectionAssert.AreEqual("\0guest\0guest"u8.ToArray(), startOk.Response);
        Assert.AreEqual("en_US", startOk.Locale);
        Assert.IsNotNull(startOk.ClientProperties.Find("product"));
        Assert.AreEqual(new Amqp0ConnectionTuneOk(100, 131072, 0), Method(connection.Writes[2]));
        Assert.AreEqual(new Amqp0ConnectionOpen("/"), Method(connection.Writes[3]));
        Assert.AreEqual(new Amqp0ChannelOpen(), Method(connection.Writes[4], 1));
        var declare = (Amqp0QueueDeclare)Method(connection.Writes[5], 1);
        Assert.AreEqual("q", declare.Queue);
        Assert.IsFalse(declare.Passive);
        Assert.IsFalse(declare.Durable);
        Assert.AreEqual(new Amqp0BasicGet("q", NoAck: true), Method(connection.Writes[6], 1));
        Assert.AreEqual(new Amqp0ChannelClose(200, "Goodbye", 0, 0), Method(connection.Writes[7], 1));
        Assert.AreEqual(new Amqp0ConnectionClose(200, "Goodbye", 0, 0), Method(connection.Writes[8]));
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Body_PublishesToDefaultExchangeSplitAtFrameMax()
    {
        byte[] body = [.. Enumerable.Repeat((byte)'x', 5000)];
        ScriptedAmqp0Connection connection = new([.. Handshake(channelMax: 0, frameMax: 4096), .. Closes()]);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("amqp://broker/%2Fprod/jobs"),
            Output = new MemoryStream(),
            PostData = body,
            Credentials = new NetworkCredential("user", "pw"),
        };

        TransferResult result = await new Amqp0ProtocolHandler(new ScriptedAmqp0Connector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual("\0user\0pw"u8.ToArray(), ((Amqp0ConnectionStartOk)Method(connection.Writes[1])).Response);
        Assert.AreEqual(new Amqp0ConnectionTuneOk(2047, 4096, 0), Method(connection.Writes[2]));
        Assert.AreEqual(new Amqp0ConnectionOpen("/prod"), Method(connection.Writes[3]));
        List<Amqp0Frame> published = Frames(connection.Writes[6]);
        Assert.HasCount(4, published);
        Assert.AreEqual(new Amqp0BasicPublish(string.Empty, "jobs"), Amqp0MethodReader.Read(published[0]));
        Assert.AreEqual(5000UL, Amqp0ContentHeader.Read(published[1].Payload).BodySize);
        Assert.HasCount(4088, published[2].Payload);
        Assert.HasCount(912, published[3].Payload);
        Assert.IsTrue(published.TrueForAll(frame => frame.Channel == 1));
    }

    [TestMethod]
    public async Task ExecuteAsync_GetEmpty_EndsAsEmptyTransfer()
    {
        ScriptedAmqp0Connection connection = new([.. Handshake(0, 0), .. Frame(new Amqp0BasicGetEmpty(), 1), .. Closes()]);
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionCloseAccessRefused_EndsWithLoginDeniedNamingReply()
    {
        ScriptedAmqp0Connection connection = new(
            [.. Frame(Start("AMQPLAIN PLAIN")), .. Frame(new Amqp0ConnectionClose(403, "ACCESS_REFUSED - Login was refused", 10, 11))]);

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("AMQP server sent connection.close: reply code 403, ACCESS_REFUSED - Login was refused", result.ErrorMessage);
        Assert.AreEqual(new Amqp0ConnectionCloseOk(), Method(connection.Writes[^1]));
    }

    [TestMethod]
    public async Task ExecuteAsync_ChannelClose_EndsWithWeirdServerReplyNamingReply()
    {
        ScriptedAmqp0Connection connection = new(
            [.. Frame(Start("PLAIN")), .. Frame(new Amqp0ConnectionTune(0, 0, 0)), .. Frame(new Amqp0ConnectionOpenOk()), .. Frame(new Amqp0ChannelOpenOk(), 1),
             .. Frame(new Amqp0ChannelClose(406, "PRECONDITION_FAILED - inequivalent arg 'durable'", 50, 10), 1)]);

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("AMQP server sent channel.close: reply code 406, PRECONDITION_FAILED - inequivalent arg 'durable'", result.ErrorMessage);
        Assert.AreEqual(new Amqp0ChannelCloseOk(), Method(connection.Writes[^1], 1));
    }

    [TestMethod]
    public async Task ExecuteAsync_CloseOkCannotBeSent_StillReportsServerClose()
    {
        ScriptedAmqp0Connection connection = new([.. Frame(new Amqp0ConnectionClose(320, "CONNECTION_FORCED", 0, 0))]) { FailsWritesAfter = 1 };

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("AMQP server sent connection.close: reply code 320, CONNECTION_FORCED", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoPlainMechanism_EndsWithLoginDenied()
    {
        ScriptedAmqp0Connection connection = new(Frame(Start("AMQPLAIN EXTERNAL")));

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("AMQP server offers no PLAIN mechanism (it offers \"AMQPLAIN EXTERNAL\")", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnexpectedMethodForStart_EndsWithWeirdServerReply()
    {
        ScriptedAmqp0Connection connection = new(Frame(new Amqp0ConnectionOpenOk()));

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply: unexpected AMQP method 10.41", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnexpectedMethodForGet_EndsWithWeirdServerReply()
    {
        ScriptedAmqp0Connection connection = new([.. Handshake(0, 0), .. Frame(new Amqp0QueueDeclareOk("q", 0, 0), 1)]);

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply: unexpected AMQP method 50.11", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadFrameEnd_EndsWithWeirdServerReplyNamingFrameError()
    {
        byte[] frame = Frame(Start("PLAIN"));
        frame[^1] = 0x00;

        TransferResult result = await ExecuteAsync(new ScriptedAmqp0Connection(frame), "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply: AMQP 501 A frame ends with 0x00, not frame-end 0xCE.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerAnswersOtherProtocolHeader_EndsWithFrameError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedAmqp0Connection("AMQP\0\0\u0009\u0000"u8.ToArray()), "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply: AMQP 501 The peer answered with protocol header 414D515000000900, not AMQP 0-9-1.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerEchoesProtocolHeader_EndsWithFrameError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedAmqp0Connection("AMQP\0\0\u0009\u0001"u8.ToArray()), "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply: AMQP 501 The peer answered with protocol header AMQP 0-9-1 instead of connection.start.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_PeerClosesFirst_EndsWithRecvError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedAmqp0Connection(), "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Connection disconnected", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFails_EndsWithRecvError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedAmqp0Connection { FailsReads = true }, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteFails_EndsWithSendError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedAmqp0Connection { FailsWritesAfter = 0 }, "amqp://broker/v/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputUnwritable_EndsWithWriteError()
    {
        ScriptedAmqp0Connection connection = new(
            [.. Handshake(0, 0), .. Frame(new Amqp0BasicGetOk(1, false, string.Empty, "q", 0), 1),
             .. Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, "abc"u8.ToArray(), 0).SelectMany(frame => frame.ToBytes())]);

        TransferResult result = await ExecuteAsync(connection, "amqp://broker/v/q", new UnwritableOutputStream());

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 3 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("amqp://broker/v/q", false, 5672)]
    [DataRow("amqps://broker/v/q", true, 5671)]
    [DataRow("AMQPS://broker:9999/v/q", true, 9999)]
    public async Task ExecuteAsync_Scheme_ConnectsWithTlsAndPortFromConnector(string url, bool useTls, int port)
    {
        ScriptedAmqp0Connector connector = new(ConnectResult.Refused("Failed to connect"));

        TransferResult result = await new Amqp0ProtocolHandler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("broker", connector.Target!.Host);
        Assert.AreEqual(port, connector.Target.Port);
        Assert.AreEqual(useTls, connector.Target.UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_Amqps_RunsTheSameFlowOverTheConnectorsTlsConnection()
    {
        ScriptedAmqp0Connection connection = new([.. Handshake(0, 0), .. Frame(new Amqp0BasicGetEmpty(), 1), .. Closes()]);
        ScriptedAmqp0Connector connector = new(ConnectResult.Connected(connection));

        TransferResult result = await new Amqp0ProtocolHandler(connector).ExecuteAsync(Context("amqps://broker/v/q", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(connector.Target!.UseTls);
        Assert.HasCount(9, connection.Writes);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoQueue_EndsWithUrlMalformatBeforeConnecting()
    {
        ScriptedAmqp0Connector connector = new(ConnectResult.Refused("x"));

        TransferResult result = await new Amqp0ProtocolHandler(connector).ExecuteAsync(Context("amqp://broker/vhost", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("No AMQP queue found: the URL path must be /<virtual host>/<queue>", result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new Amqp0ProtocolHandler(new ScriptedAmqp0Connector(ConnectResult.Refused("x"))).ExecuteAsync(null!));

    [TestMethod]
    public void SupportedSchemes_AreAmqpAndAmqps() =>
        CollectionAssert.AreEqual(new[] { "amqp", "amqps" }, new Amqp0ProtocolHandler(new ScriptedAmqp0Connector(ConnectResult.Refused("x"))).SupportedSchemes.ToArray());

    private static Amqp0ConnectionStart Start(string mechanisms) => new(0, 9, Amqp0FieldTable.Empty, mechanisms, "en_US");

    private static byte[] Handshake(ushort channelMax, uint frameMax) =>
    [
        .. Frame(Start("PLAIN AMQPLAIN")),
        .. Frame(new Amqp0ConnectionTune(channelMax, frameMax, 60)),
        .. Frame(new Amqp0ConnectionOpenOk()),
        .. Frame(new Amqp0ChannelOpenOk(), 1),
        .. Frame(new Amqp0QueueDeclareOk("q", 0, 0), 1),
    ];

    private static byte[] Closes() => [.. Frame(new Amqp0ChannelCloseOk(), 1), .. Frame(new Amqp0ConnectionCloseOk())];

    private static byte[] Frame(Amqp0Method method, ushort channel = 0) => method.ToFrame(channel).ToBytes();

    private static List<Amqp0Frame> Frames(byte[] written)
    {
        List<Amqp0Frame> frames = [];
        int offset = 0;
        while (Amqp0Frame.TryRead(written.AsSpan(offset), 0, out Amqp0Frame? frame, out int consumed))
        {
            frames.Add(frame!);
            offset += consumed;
        }

        return frames;
    }

    private static Amqp0Method Method(byte[] written, ushort channel = 0)
    {
        Amqp0Frame frame = Frames(written).Single();
        Assert.AreEqual(channel, frame.Channel);
        return Amqp0MethodReader.Read(frame);
    }

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedAmqp0Connection connection, string url, Stream output) =>
        new Amqp0ProtocolHandler(new ScriptedAmqp0Connector(ConnectResult.Connected(connection))).ExecuteAsync(Context(url, output));

    private static TransferContext Context(string url, Stream output) => new() { Url = CurlUrl.Parse(url), Output = output };
}
