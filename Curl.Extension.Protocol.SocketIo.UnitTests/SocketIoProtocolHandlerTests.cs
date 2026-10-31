using System.Text;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.Protocol.SocketIo;

/// <summary>
/// Drives <see cref="SocketIoProtocolHandler" /> and <see cref="SocketIoSession" /> through recorded server bytes:
/// Engine.IO v4 "Handshake" and "Heartbeat", Socket.IO v5 "Connection to a namespace", "Sending and receiving data",
/// "Acknowledgement" and "Disconnection from a namespace".
/// </summary>
[TestClass]
public sealed class SocketIoProtocolHandlerTests
{
    private const string Open = "0{\"sid\":\"lv_VI97HAXpY6yYWAAAC\",\"upgrades\":[],\"pingInterval\":25000,\"pingTimeout\":20000,\"maxPayload\":1000000}";

    [TestMethod]
    public async Task ExecuteAsync_TheMainNamespace_EmitsAnswersPingsAndWritesEachEvent()
    {
        ScriptedConnection connection = Server(Open, "40{\"sid\":\"x\"}", "2", "", "6", "4x", "42[\"news\",{\"a\":1}]", "421[\"question\"]", "43[]", "51-[\"bin\",{\"_placeholder\":true,\"num\":0}]", "41", "42[\"late\"]");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "socketio://example.com/", output, "[\"hello\",\"world\"]");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("[\"news\",{\"a\":1}]\n[\"question\"]\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(output.Length, result.BytesTransferred);
        CollectionAssert.AreEqual(new[] { "1:40", "1:42[\"hello\",\"world\"]", "1:3", "1:431[]" }, connection.SentFrames());
        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.Writes[0]), "GET /socket.io/?EIO=4&transport=websocket HTTP/1.1\r\nHost: example.com\r\n");
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ANamedNamespace_IgnoresOtherNamespacesAndEndsOnEngineIoClose()
    {
        ScriptedConnection connection = Server(Open, "40{\"sid\":\"main\"}", "40/chat,{\"sid\":\"x\"}", "42[\"main\"]", "42/chat,[\"chat\"]", "2probe", "1");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "socketio://example.com/chat", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("[\"chat\"]\n", Encoding.UTF8.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new[] { "1:40/chat,", "1:3probe" }, connection.SentFrames());
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectError_FailsWithExit22AndTheServersData()
    {
        ScriptedConnection connection = Server(Open, "44{\"message\":\"Not authorized\"}");

        TransferResult result = await ExecuteAsync(connection, "socketio://example.com", new MemoryStream());

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Socket.IO server refused namespace /: {\"message\":\"Not authorized\"}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedBeforeTheNamespaceConnected_FailsWithExit52()
    {
        ScriptedConnection connection = Server(Open);

        TransferResult result = await ExecuteAsync(connection, "socketio://example.com", new MemoryStream());

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Socket.IO server closed before connecting to namespace /", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataThatIsNotAnEvent_FailsWithExit2BeforeConnecting()
    {
        ScriptedConnector connector = new(ConnectResult.Connected(new ScriptedConnection()));
        SocketIoProtocolHandler handler = new(connector, new ZeroRandomSource());

        TransferResult result = await handler.ExecuteAsync(Context("socketio://example.com", new MemoryStream(), "{}"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_AConnectFailure_GivesItsExitCodeAndMessage()
    {
        SocketIoProtocolHandler handler = new(new ScriptedConnector(ConnectResult.Refused("Failed to connect")), new ZeroRandomSource());

        TransferResult result = await handler.ExecuteAsync(Context("socketio://example.com", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [DataRow("socketio://example.com", 80, false)]
    [DataRow("socketios://example.com", 443, true)]
    [DataRow("SOCKETIOS://example.com:8443", 8443, true)]
    public async Task ExecuteAsync_TheScheme_ChoosesTheDefaultPortAndTls(string url, int port, bool useTls)
    {
        ScriptedConnector connector = new(ConnectResult.Refused("refused"));

        await new SocketIoProtocolHandler(connector, new ZeroRandomSource()).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(new ConnectTarget("example.com", port, useTls), connector.Target! with { Events = NoTransferEvents.Instance, DiagnosticLog = NoDiagnosticLog.Instance });
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutAContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Refused("x")), new ZeroRandomSource()).ExecuteAsync(null!));

    [TestMethod]
    public void SupportedSchemes_AreSocketIoAndSocketIos() =>
        CollectionAssert.AreEqual(new[] { "socketio", "socketios" }, new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Refused("x")), new ZeroRandomSource()).SupportedSchemes.ToArray());

    private static ScriptedConnection Server(params string[] messages) =>
        new([.. ScriptedConnection.SwitchingProtocols(), .. messages.SelectMany(message => ScriptedConnection.ServerFrame(WsOpcode.Text, message))]);

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedConnection connection, string url, Stream output, string? emit = null) =>
        new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), new ZeroRandomSource()).ExecuteAsync(Context(url, output, emit));

    private static TransferContext Context(string url, Stream output, string? emit = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output, PostData = emit is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(emit) };
}
