using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebSocketExtendedConnect.Http2;
using Curl.Extension.WebSocketExtendedConnect.UnitTests.Http3;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using static Curl.Extension.WebSocketExtendedConnect.Http2.ScriptedServerFrames;
using InMemoryMultiplexedConnection = Curl.Extension.WebSocketExtendedConnect.UnitTests.Http3.InMemoryMultiplexedConnection;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

[TestClass]
public sealed class ExtendedConnectTransferHookTests
{
    private static readonly byte[] TextHi = [0x81, 0x02, (byte)'h', (byte)'i'];

    private static readonly byte[] CloseNormal = [0x88, 0x02, 0x03, 0xE8];

    [TestMethod]
    public async Task ExecuteAsync_HttpUrl_PassesTheTransferOnUnchanged()
    {
        ScriptedConnector connector = new();
        TransferContext context = Context("https://example.com/");

        TransferResult result = await Hook("h2").ExecuteAsync(context, Services(connector), given => ValueTask.FromResult(TransferResult.Success(given == context ? 7 : 0)));

        Assert.AreEqual(7, result.BytesTransferred);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownVersion_FailsWithFailedInit()
    {
        TransferResult result = await Hook("h1").ExecuteAsync(Context("ws://example.com/"), Services(new ScriptedConnector()), Unreached);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "h2 or h3");
    }

    [TestMethod]
    public async Task ExecuteAsync_H2OverWss_ConnectsWithAlpnH2AndRunsTheWebSocket()
    {
        ScriptedServerStream server = new(ServerSettings(EnableConnectProtocol), Response("200"), ServerData(TextHi), ServerData(CloseNormal));
        StreamConnection connection = new(server);
        ScriptedConnector connector = new(ConnectResult.Connected(connection, null, applicationProtocol: "h2"));
        TransferContext context = Context("wss://example.com:8443/chat?room=1");

        TransferResult result = await Hook("h2").ExecuteAsync(context, Services(connector), Unreached);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hi", System.Text.Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(("example.com", 8443, true), (target.Host, target.Port, target.UseTls));
        CollectionAssert.AreEqual(new[] { "h2" }, target.ApplicationProtocols!.ToArray());
        List<Http2Frame> sent = await server.ReadClientFramesAsync();
        Assert.IsTrue(sent.Any(frame => frame.Type == Http2FrameType.Data && frame.HasFlag(Http2FrameFlags.EndStream)));
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_H2OverWsServerEndsTheStream_WritesTheMessage()
    {
        ScriptedServerStream server = new(ServerSettings(EnableConnectProtocol), Response("200"), ServerData(TextHi, isEndStream: true));
        ScriptedConnector connector = new(ConnectResult.Connected(new StreamConnection(server), null));

        TransferResult result = await Hook("h2").ExecuteAsync(Context("ws://example.com/"), Services(connector), Unreached);

        Assert.AreEqual(2, result.BytesTransferred);
        Assert.IsFalse(connector.Targets.Single().UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_H2ConnectFails_GivesTheConnectorsFailure()
    {
        ScriptedConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));

        TransferResult result = await Hook("h2").ExecuteAsync(Context("ws://example.com/"), Services(connector), Unreached);

        Assert.AreEqual((CurlExitCode.CouldntConnect, "refused"), (result.ExitCode, result.ErrorMessage));
    }

    [TestMethod]
    public async Task ExecuteAsync_H2WssWithoutAlpnH2_FailsWithHttp2Error()
    {
        StreamConnection connection = new(new MemoryStream());
        ScriptedConnector connector = new(ConnectResult.Connected(connection, null, applicationProtocol: "http/1.1"));

        TransferResult result = await Hook("h2").ExecuteAsync(Context("wss://example.com/"), Services(connector), Unreached);

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "did not negotiate h2");
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_H2ServerWithoutEnableConnectProtocol_FailsWithHttp2Error()
    {
        ScriptedConnector connector = new(ConnectResult.Connected(new StreamConnection(new ScriptedServerStream(ServerSettings())), null));

        TransferResult result = await Hook("h2").ExecuteAsync(Context("ws://example.com/"), Services(connector), Unreached);

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_H3OverWs_FailsAsUnsupportedWithoutConnecting()
    {
        ScriptedConnector connector = new();

        TransferResult result = await Hook("h3").ExecuteAsync(Context("ws://example.com/"), Services(connector), Unreached);

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_H3ConnectFails_GivesTheConnectorsFailure()
    {
        ScriptedConnector connector = new(multiplexed: MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "no QUIC"));

        TransferResult result = await Hook("h3").ExecuteAsync(Context("wss://example.com/"), Services(connector), Unreached);

        Assert.AreEqual((CurlExitCode.CouldntConnect, "no QUIC"), (result.ExitCode, result.ErrorMessage));
        CollectionAssert.AreEqual(new[] { "h3" }, connector.Targets.Single().ApplicationProtocols!.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_H3OverWss_RunsTheWebSocketAnswersAndClosesTheSession()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Http3ServerBytes.EnablingControlStream());
        connection.RequestStream.Receive([.. Http3ServerBytes.Headers(new HeaderField(":status", "200")), .. Http3ServerBytes.Data([.. TextHi, 0x89, 0x00, .. CloseNormal])]);
        ScriptedConnector connector = new(multiplexed: MultiplexedConnectResult.Connected(connection, null));
        TransferContext context = Context("wss://example.com/chat");

        TransferResult result = await Hook("h3").ExecuteAsync(context, Services(connector), Unreached);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hi", System.Text.Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
        Assert.IsTrue(connection.RequestStream.IsWriteEnded);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_H3ServerWithoutEnableConnectProtocol_FailsWithHttp3Error()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Http3ServerBytes.ControlStream([new(Http3SettingIdentifier.EnableConnectProtocol, 0)]));
        ScriptedConnector connector = new(multiplexed: MultiplexedConnectResult.Connected(connection, null));

        TransferResult result = await Hook("h3").ExecuteAsync(Context("wss://example.com/"), Services(connector), Unreached);

        Assert.AreEqual(CurlExitCode.Http3, result.ExitCode);
    }

    private static ExtendedConnectTransferHook Hook(string version) => new(version, Http3ServerBytes.FixedRandomSource);

    private static TransferHookServices Services(IConnector connector) => new(connector, new NoListener());

    private static TransferContext Context(string url) => new() { Url = CurlUrl.Parse(url), Output = new MemoryStream() };

    private static ValueTask<TransferResult> Unreached(ITransferContext context) => throw new AssertFailedException("The transfer went on to the next handler.");

    private sealed class NoListener : IConnectionListener
    {
        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) => throw new AssertFailedException("The hook listened.");
    }
}
