using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>Pins how <c>-u</c> reaches the wire: the namespace CONNECT packet's <c>auth</c> payload (ADR-1311), never the log.</summary>
[TestClass]
public sealed class SocketIoCredentialsTests
{
    private const string Open = "0{\"sid\":\"s\",\"pingInterval\":25000,\"pingTimeout\":20000,\"maxPayload\":1000000}";

    [TestMethod]
    public async Task ExecuteAsync_WithCredentials_SendsThemInTheConnectPacketAuthPayload()
    {
        ScriptedConnection connection = Server(Open, "40{\"sid\":\"x\"}", "41");

        TransferResult result = await RunAsync(connection, "socketio://example.com/", new NetworkCredential("user", "pa\"ss"), new CapturingLog());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "1:40{\"username\":\"user\",\"password\":\"pa\\u0022ss\"}" }, connection.SentFrames());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithCredentialsAndANamedNamespace_PutsTheAuthAfterTheNamespaceComma()
    {
        ScriptedConnection connection = Server(Open, "40/chat,{\"sid\":\"x\"}", "41/chat,");

        await RunAsync(connection, "socketio://example.com/chat", new NetworkCredential("u", "p"), new CapturingLog());

        CollectionAssert.AreEqual(new[] { "1:40/chat,{\"username\":\"u\",\"password\":\"p\"}" }, connection.SentFrames());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutCredentials_SendsABareConnectPacket()
    {
        ScriptedConnection connection = Server(Open, "40{\"sid\":\"x\"}", "41");

        await RunAsync(connection, "socketio://example.com/", null, new CapturingLog());

        CollectionAssert.AreEqual(new[] { "1:40" }, connection.SentFrames());
    }

    [TestMethod]
    public async Task ExecuteAsync_AWrongPassword_FailsWithExit22AndTheServersReason()
    {
        ScriptedConnection connection = Server(Open, "44{\"message\":\"Invalid credentials\"}");

        TransferResult result = await RunAsync(connection, "socketio://example.com/", new NetworkCredential("user", "wrong"), new CapturingLog());

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Socket.IO server refused namespace /: {\"message\":\"Invalid credentials\"}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithCredentials_WritesNoPasswordToTheDiagnosticLogOrTheErrorMessage()
    {
        CapturingLog log = new();
        ScriptedConnection connection = Server(Open, "44{\"message\":\"no\"}");

        TransferResult result = await RunAsync(connection, "socketio://example.com/", new NetworkCredential("user", "s3cret"), log);

        Assert.DoesNotContain("s3cret", string.Join("\n", log.Messages));
        Assert.DoesNotContain("s3cret", result.ErrorMessage ?? string.Empty);
    }

    private static ScriptedConnection Server(params string[] messages) =>
        new([.. ScriptedConnection.SwitchingProtocols(), .. messages.SelectMany(message => ScriptedConnection.ServerFrame(WsOpcode.Text, message))]);

    private static ValueTask<TransferResult> RunAsync(ScriptedConnection connection, string url, NetworkCredential? credentials, IDiagnosticLog log) =>
        new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), new ZeroRandomSource())
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credentials, DiagnosticLog = log });

    private sealed class CapturingLog : IDiagnosticLog
    {
        public List<string> Messages { get; } = [];

        public bool IsEnabled(DiagnosticLogLevel level) => true;

        public void Write(DiagnosticLogLevel level, string component, string message) => Messages.Add(message);
    }
}
